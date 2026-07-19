using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;

namespace BtwDocumentDesigner.Application.Rendering;

public sealed class PdfRenderingEngine
{
    private static readonly object FontResolverLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IImageRepository _imageRepository;
    private readonly ContractValidationService _validator;

    public PdfRenderingEngine(
        IImageRepository imageRepository,
        ContractValidationService validator)
    {
        EnsureFontResolver();
        _imageRepository = imageRepository;
        _validator = validator;
    }

    private static void EnsureFontResolver()
    {
        lock (FontResolverLock)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new SystemFontResolver();
        }
    }

    public async Task<byte[]> GeneratePdfAsync(
        string jsonConfig,
        string payload,
        string contentType,
        IReadOnlyDictionary<string, JsonElement>? runtime = null)
    {
        var schema = JsonSerializer.Deserialize<PdfDesignSchema>(jsonConfig, JsonOptions)
                     ?? throw new InvalidOperationException("Diseño JSON inválido.");
        _validator.ValidateAndThrow(schema);

        XDocument? xmlData = null;
        JObject? jsonData = null;
        if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
            payload.TrimStart().StartsWith('<'))
            xmlData = XDocument.Parse(payload, LoadOptions.PreserveWhitespace);
        else
            jsonData = JObject.Parse(payload);

        using var document = new PdfDocument();
        var session = new RenderSession(
            document,
            schema,
            xmlData,
            jsonData,
            runtime ?? new Dictionary<string, JsonElement>(),
            _imageRepository);
        await session.RenderAsync();

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private sealed class RenderSession
    {
        private readonly PdfDocument _document;
        private readonly PdfDesignSchema _schema;
        private readonly XDocument? _xml;
        private readonly JObject? _json;
        private readonly IReadOnlyDictionary<string, JsonElement> _runtime;
        private readonly IImageRepository _images;
        private readonly Dictionary<string, RuntimeBounds> _bounds = [];
        private PdfPage _page = null!;
        private XGraphics _graphics = null!;
        private BindingResolver _resolver = null!;
        private int _pageIndex;

        public RenderSession(
            PdfDocument document,
            PdfDesignSchema schema,
            XDocument? xml,
            JObject? json,
            IReadOnlyDictionary<string, JsonElement> runtime,
            IImageRepository images)
        {
            _document = document;
            _schema = schema;
            _xml = xml;
            _json = json;
            _runtime = runtime;
            _images = images;
        }

        public async Task RenderAsync()
        {
            AddPage();
            _resolver.ValidateRuntime();

            foreach (var component in _schema.Components
                         .OrderBy(item => item.ZIndex)
                         .ThenBy(item => _schema.Components.IndexOf(item)))
            {
                if (IsDeferred(component)) continue;
                await RenderComponentAsync(component, 0, 0, new BindingScope());
            }

            _graphics.Dispose();
            await RenderDeferredComponentsAsync();
        }

        private void AddPage()
        {
            if (_graphics is not null) _graphics.Dispose();
            _page = _document.AddPage();
            _page.Width = XUnit.FromMillimeter(_schema.Page.WidthMm);
            _page.Height = XUnit.FromMillimeter(_schema.Page.HeightMm);
            _graphics = XGraphics.FromPdfPage(_page);
            _pageIndex = _document.PageCount - 1;
            _resolver = CreateResolver(_pageIndex + 1, Math.Max(1, _document.PageCount));

            var background = ParseColor(_schema.Page.Background, XColors.White);
            _graphics.DrawRectangle(
                new XSolidBrush(background),
                0,
                0,
                _page.Width.Point,
                _page.Height.Point);
        }

        private BindingResolver CreateResolver(int pageNumber, int totalPages) =>
            new(
                _schema,
                _xml,
                _json,
                _runtime,
                new Dictionary<string, object?>
                {
                    ["Pagina.Actual"] = pageNumber,
                    ["Pagina.Total"] = totalPages,
                    ["System.CurrentYear"] = DateTime.UtcNow.Year
                });

        private async Task RenderComponentAsync(
            PdfComponent component,
            double parentX,
            double parentY,
            BindingScope scope,
            bool deferredPass = false)
        {
            if (!component.Visible) return;
            if (component.Type == "pageBreak")
            {
                AddPage();
                return;
            }

            var xMm = parentX + component.Position.X;
            var yMm = parentY + component.Position.Y;
            if (component.Anchor?.Mode == "after" &&
                component.Anchor.ComponentId is not null &&
                _bounds.TryGetValue(component.Anchor.ComponentId, out var anchor))
            {
                if (anchor.PageIndex != _pageIndex)
                {
                    while (_pageIndex < anchor.PageIndex) AddPage();
                }
                yMm = anchor.BottomMm + component.Anchor.SpacingMm;
            }

            if (!deferredPass && parentX == 0 && parentY == 0 &&
                yMm + component.Position.Height > UsableBottomMm() &&
                component.Type is not "table")
            {
                AddPage();
                yMm = _schema.Page.MarginsMm.Top;
            }

            var style = EffectiveStyle(component);
            var x = MmToPt(xMm);
            var y = MmToPt(yMm);
            var width = MmToPt(component.Position.Width);
            var height = MmToPt(component.Position.Height);
            var state = _graphics.Save();
            if (component.RotationDegrees is { } rotation and not 0)
                _graphics.RotateAtTransform(rotation, new XPoint(x + width / 2, y + height / 2));

            switch (component.Type)
            {
                case "text":
                case "link":
                    DrawText(
                        _resolver.Render(
                            component.Content.Value,
                            scope,
                            component.Content.Bindings,
                            JsonText(component.Content.DefaultValue)),
                        new XRect(x, y, width, height),
                        style);
                    break;
                case "pageNumber":
                    DrawText(
                        _resolver.Render(component.Content.Format ?? component.Content.Value, scope),
                        new XRect(x, y, width, height),
                        style);
                    break;
                case "image":
                case "qrCode":
                    await DrawImageAsync(component, new XRect(x, y, width, height), scope);
                    break;
                case "line":
                    DrawLine(component, x, y, width, height, style);
                    break;
                case "rectangle":
                    DrawBox(new XRect(x, y, width, height), style);
                    break;
                case "barcode":
                    DrawBarcode(component, new XRect(x, y, width, height), scope, style);
                    break;
                case "container":
                    DrawBox(new XRect(x, y, width, height), style);
                    foreach (var child in component.Components
                                 .OrderBy(item => item.ZIndex)
                                 .ThenBy(item => component.Components.IndexOf(item)))
                        await RenderComponentAsync(child, xMm, yMm, scope, deferredPass);
                    break;
                case "table":
                    yMm = await DrawTableAsync(component, xMm, yMm, scope, style);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Renderer no implementado para '{component.Type}' ({component.Id}).");
            }
            _graphics.Restore(state);

            var bottom = component.Type == "table"
                ? yMm
                : yMm + component.Position.Height;
            _bounds[component.Id] = new RuntimeBounds(_pageIndex, bottom);
        }

        private async Task<double> DrawTableAsync(
            PdfComponent component,
            double xMm,
            double yMm,
            BindingScope scope,
            ComponentStyle style)
        {
            var columns = component.Columns;
            if (columns.Count == 0) return yMm;
            var headerHeight = component.Behavior.HeaderHeightMm ?? 6;
            var rowHeight = component.Style.RowHeightMm ??
                            component.Behavior.MinRowHeightMm ?? 6;
            var currentY = yMm;

            void EnsureSpace(double required, bool drawHeader)
            {
                var reserved = component.Behavior.ReservedBottomSpaceMm ?? 0;
                if (currentY + required <= UsableBottomMm() - reserved) return;
                AddPage();
                currentY = _schema.Page.MarginsMm.Top;
                if (drawHeader) DrawTableHeader(component, columns, xMm, currentY, headerHeight, style);
                if (drawHeader) currentY += headerHeight;
            }

            if (columns.Any(column => !string.IsNullOrWhiteSpace(column.Title)))
            {
                EnsureSpace(headerHeight + rowHeight, false);
                DrawTableHeader(component, columns, xMm, currentY, headerHeight, style);
                currentY += headerHeight;
            }

            if (component.Content.Mode == "collection")
            {
                var path = component.Content.CollectionPath ?? component.Content.DataPath ?? string.Empty;
                var records = _resolver.Collection(path);
                var alias = component.Content.RowAlias ?? "Row";
                var index = 0;
                foreach (var record in records)
                {
                    EnsureSpace(rowHeight, component.Behavior.RepeatHeader == true);
                    var rowScope = new BindingScope(
                        record,
                        alias,
                        new Dictionary<string, object?>
                        {
                            ["Iteration.Index"] = ++index,
                            ["Table.RecordCount"] = records.Count
                        });
                    DrawTableRow(columns, xMm, currentY, rowHeight, style, rowScope);
                    currentY += rowHeight;
                }
                if (component.Content.ShowRecordCount)
                {
                    var text = _resolver.Render(
                        component.Content.RecordCountLabel,
                        new BindingScope(
                            LocalValues: new Dictionary<string, object?>
                            {
                                ["Table.RecordCount"] = records.Count
                            }));
                    DrawText(
                        text,
                        new XRect(
                            MmToPt(xMm),
                            MmToPt(currentY),
                            MmToPt(component.Position.Width),
                            MmToPt(4)),
                        style);
                    currentY += 4;
                }
                return currentY;
            }

            if (component.Content.Mode == "record")
            {
                EnsureSpace(rowHeight, false);
                var values = columns.Select(column =>
                {
                    var field = component.Content.Fields.FirstOrDefault(item => item.Column == column.Id);
                    return field is null
                        ? string.Empty
                        : _resolver.Render(
                            field.Value ?? PathTemplate(field.DataPath),
                            scope,
                            defaultValue: field.DefaultValue);
                }).ToArray();
                DrawTableRow(columns, values, xMm, currentY, rowHeight, style);
                return currentY + rowHeight;
            }

            var rows = DeserializeRows(component.Content.Rows);
            foreach (var row in rows)
            {
                EnsureSpace(rowHeight, false);
                var values = columns.Select((column, columnIndex) =>
                    columnIndex == 0
                        ? _resolver.Render(row.Label, scope)
                        : _resolver.Render(
                            row.Value ?? PathTemplate(row.DataPath),
                            scope,
                            defaultValue: row.DefaultValue)).ToArray();
                DrawTableRow(columns, values, xMm, currentY, rowHeight, style);
                currentY += rowHeight;
            }
            return currentY;
        }

        private void DrawTableHeader(
            PdfComponent component,
            IReadOnlyList<TableColumn> columns,
            double xMm,
            double yMm,
            double heightMm,
            ComponentStyle style)
        {
            var header = style.Merge(new ComponentStyle
            {
                Bold = style.Header?.Bold ?? style.HeaderBold ?? true,
                Alignment = style.Header?.Alignment ?? "center",
                Background = style.Header?.Background
            });
            DrawTableRow(
                columns,
                columns.Select(column => column.Title ?? string.Empty).ToArray(),
                xMm,
                yMm,
                heightMm,
                header);
        }

        private void DrawTableRow(
            IReadOnlyList<TableColumn> columns,
            double xMm,
            double yMm,
            double heightMm,
            ComponentStyle style,
            BindingScope scope)
        {
            var values = columns.Select(column =>
            {
                var value = _resolver.Render(
                    column.Value ?? PathTemplate(column.DataPath),
                    scope,
                    defaultValue: column.DefaultValue);
                if (string.IsNullOrWhiteSpace(value) && column.Fallback?.DataPath is not null)
                    value = _resolver.Render(
                        PathTemplate(column.Fallback.DataPath),
                        scope,
                        defaultValue: column.Fallback.DefaultValue);
                return value;
            }).ToArray();
            DrawTableRow(columns, values, xMm, yMm, heightMm, style);
        }

        private void DrawTableRow(
            IReadOnlyList<TableColumn> columns,
            IReadOnlyList<string> values,
            double xMm,
            double yMm,
            double heightMm,
            ComponentStyle style)
        {
            var currentX = xMm;
            for (var index = 0; index < columns.Count; index++)
            {
                var column = columns[index];
                var widthMm = column.WidthMm ?? 0;
                var cellRect = new XRect(
                    MmToPt(currentX),
                    MmToPt(yMm),
                    MmToPt(widthMm),
                    MmToPt(heightMm));
                var cellStyle = style.Merge(column.Style).Merge(
                    new ComponentStyle { Alignment = column.Alignment });
                DrawBox(cellRect, cellStyle);
                DrawText(values.ElementAtOrDefault(index) ?? string.Empty, cellRect, cellStyle);
                currentX += widthMm;
            }
        }

        private async Task DrawImageAsync(
            PdfComponent component,
            XRect rect,
            BindingScope scope)
        {
            var data = await ResolveImageDataAsync(component, scope);
            if (data is null || data.Length == 0)
            {
                if (component.Required)
                    throw new InvalidOperationException(
                        $"No se pudo resolver la imagen obligatoria '{component.Id}'.");
                return;
            }

            using var stream = new MemoryStream(data);
            using var image = XImage.FromStream(stream);
            var target = FitRect(rect, image, component.Content.Fit);
            _graphics.DrawImage(image, target);
        }

        private async Task<byte[]?> ResolveImageDataAsync(
            PdfComponent component,
            BindingScope scope)
        {
            if (!string.IsNullOrWhiteSpace(component.Content.DataPath))
            {
                var value = _resolver.Resolve(component.Content.DataPath, scope);
                if (value is byte[] bytes) return bytes;
                if (value is string encoded) return DecodeImage(encoded);
            }

            var resource = _schema.Resources.FirstOrDefault(
                item => item.Id == component.Content.AssetId);
            if (!string.IsNullOrWhiteSpace(resource?.Data))
                return DecodeImage(resource.Data);
            if (Guid.TryParse(component.Content.AssetId, out var id))
                return await _images.GetImageDataAsync(id);
            if (!string.IsNullOrWhiteSpace(component.Content.AssetId))
                return await _images.GetImageDataByKeyAsync(component.Content.AssetId);
            return null;
        }

        private void DrawBarcode(
            PdfComponent component,
            XRect rect,
            BindingScope scope,
            ComponentStyle style)
        {
            var value = _resolver.Render(
                component.Content.Value ?? PathTemplate(component.Content.DataPath),
                scope);
            if (string.IsNullOrEmpty(value)) return;
            var bits = string.Concat(
                value.Select(character => Convert.ToString(character, 2).PadLeft(8, '0')));
            var barWidth = rect.Width / Math.Max(1, bits.Length);
            for (var index = 0; index < bits.Length; index++)
            {
                if (bits[index] == '1')
                    _graphics.DrawRectangle(
                        XBrushes.Black,
                        rect.X + index * barWidth,
                        rect.Y,
                        Math.Max(0.5, barWidth),
                        rect.Height * 0.8);
            }
            DrawText(value, new XRect(rect.X, rect.Y + rect.Height * 0.8, rect.Width, rect.Height * 0.2), style);
        }

        private void DrawText(string text, XRect rect, ComponentStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            var font = CreateFont(style);
            var brush = new XSolidBrush(ParseColor(style.Color, XColors.Black));
            var padding = MmToPt(style.Padding ?? 0);
            var contentRect = new XRect(
                rect.X + padding,
                rect.Y + padding,
                Math.Max(0, rect.Width - padding * 2),
                Math.Max(0, rect.Height - padding * 2));
            var formatter = new XTextFormatter(_graphics)
            {
                Alignment = style.Alignment switch
                {
                    "center" => XParagraphAlignment.Center,
                    "right" => XParagraphAlignment.Right,
                    "justify" => XParagraphAlignment.Justify,
                    _ => XParagraphAlignment.Left
                }
            };
            formatter.DrawString(text, font, brush, contentRect);
        }

        private void DrawBox(XRect rect, ComponentStyle style)
        {
            if (!string.IsNullOrWhiteSpace(style.Background) &&
                !style.Background.Equals("transparent", StringComparison.OrdinalIgnoreCase))
                _graphics.DrawRectangle(
                    new XSolidBrush(ParseColor(style.Background, XColors.White)),
                    rect);

            var border = style.Border;
            if (border is null || border.Style == "none" || (border.WidthPt ?? 0) <= 0) return;
            var pen = new XPen(
                ParseColor(border.Color, XColors.Black),
                border.WidthPt ?? 0.5);
            pen.DashStyle = border.Style switch
            {
                "dashed" => XDashStyle.Dash,
                "dotted" => XDashStyle.Dot,
                _ => XDashStyle.Solid
            };
            if ((border.RadiusMm ?? 0) > 0)
            {
                var radius = MmToPt(border.RadiusMm!.Value);
                _graphics.DrawRoundedRectangle(pen, rect, new XSize(radius, radius));
            }
            else
                _graphics.DrawRectangle(pen, rect);
        }

        private void DrawLine(
            PdfComponent component,
            double x,
            double y,
            double width,
            double height,
            ComponentStyle style)
        {
            var pen = new XPen(
                ParseColor(style.Color ?? style.Border?.Color, XColors.Black),
                style.WidthPt ?? style.Border?.WidthPt ?? 0.5);
            pen.DashStyle = style.Style == "dashed" ? XDashStyle.Dash : XDashStyle.Solid;
            _graphics.DrawLine(pen, x, y, x + width, y + height);
        }

        private async Task RenderDeferredComponentsAsync()
        {
            var totalPages = _document.PageCount;
            for (var index = 0; index < totalPages; index++)
            {
                _page = _document.Pages[index];
                _pageIndex = index;
                _graphics = XGraphics.FromPdfPage(_page, XGraphicsPdfPageOptions.Append);
                _resolver = CreateResolver(index + 1, totalPages);

                foreach (var component in _schema.Components.Where(
                             item => IsRepeatedPlacement(item, index, totalPages)))
                    await RenderComponentAsync(component, 0, 0, new BindingScope(), true);

                foreach (var component in _schema.Components)
                    await RenderNestedPageNumbersAsync(component, 0, 0, index, totalPages);
                _graphics.Dispose();
            }
        }

        private async Task RenderNestedPageNumbersAsync(
            PdfComponent component,
            double parentX,
            double parentY,
            int pageIndex,
            int totalPages)
        {
            var repeat = component.Behavior.RepeatOn;
            if (repeat == "firstPage" && pageIndex > 0) return;
            if (repeat == "lastPage" && pageIndex != totalPages - 1) return;
            var x = parentX + component.Position.X;
            var y = parentY + component.Position.Y;
            foreach (var child in component.Components)
            {
                if (child.Type == "pageNumber")
                    await RenderComponentAsync(child, x, y, new BindingScope(), true);
                else
                    await RenderNestedPageNumbersAsync(child, x, y, pageIndex, totalPages);
            }
        }

        private bool IsDeferred(PdfComponent component) =>
            component.Behavior.Placement is "pageFooter" or "pageHeader";

        private static bool IsRepeatedPlacement(
            PdfComponent component,
            int pageIndex,
            int totalPages)
        {
            if (component.Behavior.Placement is not ("pageFooter" or "pageHeader"))
                return false;
            return component.Behavior.RepeatOn switch
            {
                "firstPage" => pageIndex == 0,
                "lastPage" => pageIndex == totalPages - 1,
                _ => true
            };
        }

        private ComponentStyle EffectiveStyle(PdfComponent component)
        {
            var baseStyle = component.StyleRef is not null &&
                            _schema.SharedStyles.TryGetValue(component.StyleRef, out var shared)
                ? shared
                : new ComponentStyle();
            return baseStyle.Merge(component.Style);
        }

        private double UsableBottomMm() =>
            _schema.Page.HeightMm - _schema.Page.MarginsMm.Bottom;

        private static XFont CreateFont(ComponentStyle style)
        {
            var bold = style.Bold == true;
            var italic = style.Italic == true;
            var fontStyle = (bold, italic) switch
            {
                (true, true) => XFontStyleEx.BoldItalic,
                (true, false) => XFontStyleEx.Bold,
                (false, true) => XFontStyleEx.Italic,
                _ => XFontStyleEx.Regular
            };
            return new XFont(style.FontFamily ?? "Arial", style.FontSizePt ?? 10, fontStyle);
        }

        private static XRect FitRect(XRect target, XImage image, string? fit)
        {
            if (fit == "fill" || image.PointWidth <= 0 || image.PointHeight <= 0) return target;
            var scale = fit == "cover"
                ? Math.Max(target.Width / image.PointWidth, target.Height / image.PointHeight)
                : Math.Min(target.Width / image.PointWidth, target.Height / image.PointHeight);
            var width = image.PointWidth * scale;
            var height = image.PointHeight * scale;
            return new XRect(
                target.X + (target.Width - width) / 2,
                target.Y + (target.Height - height) / 2,
                width,
                height);
        }

        private static byte[]? DecodeImage(string encoded)
        {
            try
            {
                var comma = encoded.IndexOf(',');
                var base64 = encoded.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0
                    ? encoded[(comma + 1)..]
                    : encoded;
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static List<TableRow> DeserializeRows(JsonElement? rows)
        {
            if (rows is null || rows.Value.ValueKind != JsonValueKind.Array) return [];
            return JsonSerializer.Deserialize<List<TableRow>>(
                       rows.Value.GetRawText(),
                       JsonOptions) ?? [];
        }

        private static string? JsonText(JsonElement? element) =>
            element is null
                ? null
                : element.Value.ValueKind == JsonValueKind.String
                    ? element.Value.GetString()
                    : element.Value.ToString();

        private static string? PathTemplate(string? path) =>
            string.IsNullOrWhiteSpace(path) ? null : $"{{{{{path}}}}}";

        private static double MmToPt(double millimeters) =>
            millimeters * 72d / 25.4d;

        private static XColor ParseColor(string? value, XColor fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('#')) return fallback;
            try
            {
                var hex = value[1..];
                if (hex.Length == 3)
                    hex = string.Concat(hex.Select(character => $"{character}{character}"));
                return hex.Length == 6
                    ? XColor.FromArgb(
                        Convert.ToInt32(hex[..2], 16),
                        Convert.ToInt32(hex.Substring(2, 2), 16),
                        Convert.ToInt32(hex.Substring(4, 2), 16))
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private sealed record RuntimeBounds(int PageIndex, double BottomMm);
    }
}
