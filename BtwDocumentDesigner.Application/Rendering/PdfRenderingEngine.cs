using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BtwDocumentDesigner.Application.Interfaces;
using PdfSharp.Drawing.Layout;

namespace BtwDocumentDesigner.Application.Rendering
{
    public sealed class PdfRenderingEngine
    {
        private static readonly Regex BindingRegex =
            new(@"\{\{(.+?)\}\}", RegexOptions.Compiled);

        private readonly IImageRepository _imageRepository;
        private readonly ISystemDefaultValueRepository _systemDefaultValueRepository;

        public PdfRenderingEngine(
            IImageRepository imageRepository,
            ISystemDefaultValueRepository systemDefaultValueRepository)
        {
            _imageRepository = imageRepository;
            _systemDefaultValueRepository = systemDefaultValueRepository;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
            {
                PdfSharp.Fonts.GlobalFontSettings.FontResolver =
                    new CrossPlatformFontResolver();
            }
        }

        public async Task<byte[]> GeneratePdfAsync(
            string jsonConfig,
            string payload,
            string contentType)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var schema = JsonSerializer.Deserialize<PdfDesignSchema>(
                jsonConfig,
                options)
                ?? throw new InvalidOperationException(
                    "Diseño JSON inválido.");
            var systemDefaults = await _systemDefaultValueRepository.GetAllAsync();
            var context = BindingContext.Create(payload, contentType, systemDefaults);
            var designPages = ResolveDesignPages(schema);

            using var document = new PdfDocument();
            var totalPages = designPages.Count;

            for (var index = 0; index < designPages.Count; index++)
            {
                var designPage = designPages[index];
                var page = AddPage(document, designPage.Page);
                using var gfx = XGraphics.FromPdfPage(page);

                var pageBrush = new XSolidBrush(
                    ParseColor(designPage.Page.Background));
                gfx.DrawRectangle(
                    pageBrush,
                    0,
                    0,
                    page.Width.Point,
                    page.Height.Point);

                var rootPositions = ResolveRootPositions(
                    designPage.Components,
                    context);
                await RenderComponents(
                    gfx,
                    designPage.Components,
                    context.WithPage(index + 1, totalPages),
                    rootPositions);
            }

            using var stream = new MemoryStream();
            document.Save(stream, false);
            return stream.ToArray();
        }

        /// <summary>
        /// Usa <c>pages</c> cuando existe; si no, cae a la página raíz legacy.
        /// </summary>
        private static List<PdfDesignPage> ResolveDesignPages(
            PdfDesignSchema schema)
        {
            if (schema.Pages is { Count: > 0 })
            {
                return schema.Pages;
            }

            return
            [
                new PdfDesignPage
                {
                    Id = "page-1",
                    Page = schema.Page,
                    Components = schema.Components
                }
            ];
        }

        private static PdfPage AddPage(
            PdfDocument document,
            PageSettings settings)
        {
            var page = document.AddPage();
            var width = settings.WidthMm > 0 ? settings.WidthMm : 210;
            var height = settings.HeightMm > 0 ? settings.HeightMm : 297;
            var landscape = string.Equals(
                settings.Orientation,
                "landscape",
                StringComparison.OrdinalIgnoreCase);

            // El editor ya envía widthMm/heightMm según la orientación.
            // Solo intercambiar si las dimensiones aún no coinciden.
            if (landscape && width < height)
            {
                (width, height) = (height, width);
            }
            else if (!landscape && width > height)
            {
                (width, height) = (height, width);
            }

            page.Width = XUnit.FromMillimeter(width);
            page.Height = XUnit.FromMillimeter(height);
            return page;
        }

        private static Dictionary<string, Position> ResolveRootPositions(
            IReadOnlyList<PdfComponent> components,
            BindingContext context)
        {
            var positions = new Dictionary<string, Position>(
                StringComparer.OrdinalIgnoreCase);
            var heights = new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var component in components)
            {
                var position = Clone(component.Position);
                if (
                    component.Anchor?.Mode == "after"
                    && component.Anchor.ComponentId is { Length: > 0 } targetId
                    && positions.TryGetValue(targetId, out var targetPosition)
                )
                {
                    position.Y =
                        targetPosition.Y
                        + heights.GetValueOrDefault(
                            targetId,
                            targetPosition.Height)
                        + component.Anchor.SpacingMm;
                }

                positions[component.Id] = position;
                heights[component.Id] = GetRenderedHeightMm(component, context);
            }

            return positions;
        }

        private static double GetRenderedHeightMm(
            PdfComponent component,
            BindingContext context)
        {
            if (
                component.Type != "table"
                || component.Content.Mode != "collection"
            )
            {
                return component.Position.Height;
            }

            var count = context.Collection(component.Content.DataPath).Count;
            var headerHeight = component.Behavior.HeaderHeightMm > 0
                ? component.Behavior.HeaderHeightMm
                : 7;
            var rowHeight = component.Behavior.MinRowHeightMm > 0
                ? component.Behavior.MinRowHeightMm
                : 8;
            var countHeight = component.Content.ShowRecordCount ? 5 : 0;
            return Math.Max(
                component.Position.Height,
                headerHeight + count * rowHeight + countHeight);
        }

        private async Task RenderComponents(
            XGraphics gfx,
            IReadOnlyList<PdfComponent> components,
            BindingContext context,
            IReadOnlyDictionary<string, Position>? resolvedPositions = null,
            double parentX = 0,
            double parentY = 0)
        {
            foreach (var component in components)
            {
                var position =
                    resolvedPositions?.GetValueOrDefault(component.Id)
                    ?? component.Position;
                var rect = new XRect(
                    parentX + MmToPt(position.X),
                    parentY + MmToPt(position.Y),
                    MmToPt(position.Width),
                    MmToPt(position.Height));
                var renderRect = rect;
                var state = gfx.Save();

                if (component.RotationDegrees is { } rotation && rotation != 0)
                {
                    var center = new XPoint(
                        rect.X + rect.Width / 2,
                        rect.Y + rect.Height / 2);
                    gfx.RotateAtTransform(
                        rotation,
                        center);

                    if (Math.Abs(Math.Abs(rotation % 180) - 90) < 0.001)
                    {
                        renderRect = new XRect(
                            center.X - rect.Height / 2,
                            center.Y - rect.Width / 2,
                            rect.Height,
                            rect.Width);
                    }
                }

                switch (component.Type)
                {
                    case "text":
                    case "pageNumber":
                        DrawText(gfx, component, renderRect, context);
                        break;
                    case "image":
                    case "qrCode":
                        await DrawImage(gfx, component, renderRect, context);
                        break;
                    case "container":
                        DrawBox(gfx, renderRect, component.Style);
                        await RenderComponents(
                            gfx,
                            component.Components,
                            context,
                            parentX: renderRect.X,
                            parentY: renderRect.Y);
                        break;
                    case "table":
                        DrawTable(gfx, component, renderRect, context);
                        break;
                    case "rectangle":
                        DrawBox(gfx, renderRect, component.Style);
                        break;
                    case "line":
                        DrawLine(gfx, component, renderRect);
                        break;
                }

                gfx.Restore(state);
            }
        }

        private static void DrawText(
            XGraphics gfx,
            PdfComponent component,
            XRect rect,
            BindingContext context)
        {
            var template = component.Type == "pageNumber"
                ? component.Content.Format
                : component.Content.Value;
            var text = context.Render(template);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = context.Render(component.Content.DefaultValue);
            }

            text = RepairMojibake(text);
            DrawTextInRect(gfx, text, rect, component.Style);
        }

        private static void DrawTextInRect(
            XGraphics gfx,
            string text,
            XRect rect,
            ComponentStyle style,
            bool? forceBold = null,
            string? forceAlignment = null)
        {
            text = RepairMojibake(text);
            if (!string.IsNullOrWhiteSpace(style.Background))
            {
                gfx.DrawRectangle(
                    new XSolidBrush(ParseColor(style.Background)),
                    rect);
            }

            var fontSize = style.FontSizePt > 0 ? style.FontSizePt : 10;
            var fontStyle = (forceBold ?? style.Bold)
                ? XFontStyleEx.Bold
                : XFontStyleEx.Regular;
            if (style.Underline)
            {
                fontStyle |= XFontStyleEx.Underline;
            }

            var font = CreateFont(style.FontFamily, fontSize, fontStyle);
            var brush = new XSolidBrush(ParseColor(style.Color));
            var padding = MmToPt(style.Padding);
            var contentRect = new XRect(
                rect.X + padding,
                rect.Y + padding,
                Math.Max(0, rect.Width - padding * 2),
                Math.Max(0, rect.Height - padding * 2));
            var alignment = forceAlignment ?? style.Alignment;
            if (
                !text.Any(char.IsWhiteSpace)
                && gfx.MeasureString(text, font).Width > contentRect.Width
            )
            {
                var breakAt = text.IndexOf('_', StringComparison.Ordinal);
                if (breakAt > 0 && breakAt < text.Length - 1)
                {
                    text = text.Insert(breakAt + 1, "\n");
                }
            }

            if (text.Contains('\n') || NeedsWrapping(gfx, text, font, contentRect))
            {
                var formatter = new XTextFormatter(gfx)
                {
                    Alignment = ToParagraphAlignment(alignment)
                };
                formatter.DrawString(text, font, brush, contentRect);
                return;
            }

            var format = new XStringFormat
            {
                Alignment = ToStringAlignment(alignment),
                LineAlignment = ToLineAlignment(style.VerticalAlignment)
            };
            gfx.DrawString(text, font, brush, contentRect, format);
        }

        private async Task DrawImage(
            XGraphics gfx,
            PdfComponent component,
            XRect rect,
            BindingContext context)
        {
            byte[]? data = null;
            var dynamicValue = context.Resolve(component.Content.DataPath);
            if (dynamicValue is string encoded && !string.IsNullOrWhiteSpace(encoded))
            {
                data = DecodeImage(encoded);
            }

            if (
                data == null
                && Guid.TryParse(component.Content.AssetId, out var assetId)
            )
            {
                data = await _imageRepository.GetImageDataAsync(assetId);
            }

            if (data == null || data.Length == 0)
            {
                return;
            }

            using var imageStream = new MemoryStream(data);
            using var image = XImage.FromStream(imageStream);
            var target = FitImage(
                rect,
                image.PointWidth,
                image.PointHeight,
                component.Content.Fit ?? component.Style.Fit);
            gfx.DrawImage(image, target);
        }

        private static void DrawLine(
            XGraphics gfx,
            PdfComponent component,
            XRect rect)
        {
            var pen = CreatePen(component.Style);
            gfx.DrawLine(pen, rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
        }

        private static void DrawBox(
            XGraphics gfx,
            XRect rect,
            ComponentStyle style)
        {
            if (!string.IsNullOrWhiteSpace(style.Background))
            {
                gfx.DrawRectangle(
                    new XSolidBrush(ParseColor(style.Background)),
                    rect);
            }

            if (style.Border.Style != "none" && style.Border.WidthPt > 0)
            {
                gfx.DrawRectangle(CreatePen(style.Border), rect);
            }
        }

        private static void DrawTable(
            XGraphics gfx,
            PdfComponent table,
            XRect rect,
            BindingContext context)
        {
            switch (table.Content.Mode)
            {
                case "fixedRows":
                    DrawFixedRows(gfx, table, rect, context);
                    break;
                case "record":
                    DrawRecord(gfx, table, rect, context);
                    break;
                case "collection":
                    DrawCollection(gfx, table, rect, context);
                    break;
            }
        }

        private static void DrawFixedRows(
            XGraphics gfx,
            PdfComponent table,
            XRect rect,
            BindingContext context)
        {
            var rows = table.Content.Rows
                .Concat(
                    table.Content.OptionalRows.Where(
                        row => context.Evaluate(row.VisibilityCondition)))
                .ToList();
            if (rows.Count == 0)
            {
                return;
            }

            var columns = NormalizeColumns(table.Columns, rect.Width);
            var rowHeight = table.Style.RowHeightMm > 0
                ? MmToPt(table.Style.RowHeightMm)
                : rect.Height / rows.Count;
            var y = rect.Y;

            foreach (var row in rows)
            {
                var values = new[]
                {
                    context.Render(row.Label),
                    ResolveWithDefault(
                        context,
                        row.Value ?? Wrap(row.DataPath),
                        row.DefaultValue)
                };
                DrawTableRow(
                    gfx,
                    rect.X,
                    y,
                    rowHeight,
                    columns,
                    values,
                    table,
                    header: false);
                y += rowHeight;
            }
        }

        private static void DrawRecord(
            XGraphics gfx,
            PdfComponent table,
            XRect rect,
            BindingContext context)
        {
            var columns = NormalizeColumns(table.Columns, rect.Width);
            var values = table.Columns
                .Select(
                    column =>
                    {
                        var field = table.Content.Fields.FirstOrDefault(
                            item => item.Column == column.Id);
                        return ResolveWithDefault(
                            context,
                            field?.Value ?? Wrap(field?.DataPath),
                            field?.DefaultValue);
                    })
                .ToArray();
            DrawTableRow(
                gfx,
                rect.X,
                rect.Y,
                rect.Height,
                columns,
                values,
                table,
                header: true);
        }

        private static void DrawCollection(
            XGraphics gfx,
            PdfComponent table,
            XRect rect,
            BindingContext context)
        {
            var columns = NormalizeColumns(table.Columns, rect.Width);
            var headerHeight = MmToPt(
                table.Behavior.HeaderHeightMm > 0
                    ? table.Behavior.HeaderHeightMm
                    : 7);
            var rowHeight = MmToPt(
                table.Behavior.MinRowHeightMm > 0
                    ? table.Behavior.MinRowHeightMm
                    : table.RowTemplate.MinHeightMm);
            if (rowHeight <= 0)
            {
                rowHeight = MmToPt(8);
            }

            DrawTableRow(
                gfx,
                rect.X,
                rect.Y,
                headerHeight,
                columns,
                table.Columns.Select(column => column.Title ?? string.Empty).ToArray(),
                table,
                header: true,
                titlesOnly: true);

            var records = context.Collection(table.Content.DataPath);
            var alias = table.Content.RowAlias ?? "Row";
            var y = rect.Y + headerHeight;
            for (var index = 0; index < records.Count; index++)
            {
                var rowContext = context.WithAlias(alias, records[index], index + 1);
                var values = table.Columns
                    .Select(
                        column =>
                        {
                            var value = ResolveWithDefault(
                                rowContext,
                                column.Value ?? Wrap(column.DataPath),
                                column.DefaultValue);
                            if (
                                string.IsNullOrWhiteSpace(value)
                                && column.Fallback?.DataPath is { Length: > 0 } fallback
                            )
                            {
                                value = rowContext.Render(Wrap(fallback));
                            }
                            return value;
                        })
                    .ToArray();
                DrawTableRow(
                    gfx,
                    rect.X,
                    y,
                    rowHeight,
                    columns,
                    values,
                    table,
                    header: false);
                y += rowHeight;
            }

            if (table.Content.ShowRecordCount)
            {
                var countContext = context.WithTableRecordCount(records.Count);
                var label = countContext.Render(table.Content.RecordCountLabel);
                var labelRect = new XRect(
                    rect.X,
                    y + MmToPt(1),
                    rect.Width,
                    MmToPt(4));
                DrawTextInRect(gfx, label, labelRect, table.Style);
            }
        }

        private static void DrawTableRow(
            XGraphics gfx,
            double x,
            double y,
            double height,
            IReadOnlyList<double> widths,
            IReadOnlyList<string> values,
            PdfComponent table,
            bool header,
            bool titlesOnly = false)
        {
            for (var index = 0; index < widths.Count; index++)
            {
                var cell = new XRect(x, y, widths[index], height);
                var cellState = gfx.Save();
                gfx.IntersectClip(cell);
                var column = table.Columns.ElementAtOrDefault(index);
                var background = header
                    ? table.Style.Header.Background
                    : column?.Style.Background;
                if (!string.IsNullOrWhiteSpace(background))
                {
                    gfx.DrawRectangle(
                        new XSolidBrush(ParseColor(background)),
                        cell);
                }

                if (table.Style.Border.Style != "none")
                {
                    gfx.DrawRectangle(CreatePen(table.Style.Border), cell);
                }

                if (header && !titlesOnly)
                {
                    var titleRect = new XRect(
                        cell.X + MmToPt(0.6),
                        cell.Y + MmToPt(0.4),
                        Math.Max(0, cell.Width - MmToPt(1.2)),
                        cell.Height * 0.48 - MmToPt(0.2));
                    DrawTextInRect(
                        gfx,
                        column?.Title ?? string.Empty,
                        titleRect,
                        table.Style,
                        forceBold: true,
                        forceAlignment: "center");
                    var valueRect = new XRect(
                        cell.X + MmToPt(0.6),
                        cell.Y + cell.Height * 0.42,
                        Math.Max(0, cell.Width - MmToPt(1.2)),
                        cell.Height * 0.54 - MmToPt(0.2));
                    DrawTextInRect(
                        gfx,
                        values.ElementAtOrDefault(index) ?? string.Empty,
                        valueRect,
                        table.Style,
                        forceBold: true,
                        forceAlignment: column?.Alignment ?? table.Style.Alignment);
                }
                else
                {
                    var textRect = new XRect(
                        cell.X + MmToPt(0.6),
                        cell.Y + MmToPt(0.3),
                        Math.Max(0, cell.Width - MmToPt(1.2)),
                        Math.Max(0, cell.Height - MmToPt(0.6)));
                    DrawTextInRect(
                        gfx,
                        values.ElementAtOrDefault(index) ?? string.Empty,
                        textRect,
                        table.Style,
                        forceBold:
                            header
                            || table.Style.Header.Bold
                            || column?.Style.Bold == true,
                        forceAlignment:
                            header
                                ? table.Style.Header.Alignment
                                : column?.Alignment
                                    ?? column?.Style.Alignment
                                    ?? table.Style.Alignment);
                }

                gfx.Restore(cellState);
                x += widths[index];
            }
        }

        private static IReadOnlyList<double> NormalizeColumns(
            IReadOnlyList<TableColumn> columns,
            double totalWidth)
        {
            var declared = columns.Sum(column => Math.Max(0, column.WidthMm));
            if (declared <= 0)
            {
                return Enumerable.Repeat(
                    totalWidth / Math.Max(1, columns.Count),
                    columns.Count).ToArray();
            }

            return columns
                .Select(column => totalWidth * column.WidthMm / declared)
                .ToArray();
        }

        private static string ResolveWithDefault(
            BindingContext context,
            string? template,
            string? defaultValue)
        {
            var value = context.Render(template);
            return string.IsNullOrWhiteSpace(value)
                ? context.Render(defaultValue)
                : value;
        }

        private static string? Wrap(string? path) =>
            string.IsNullOrWhiteSpace(path) ? null : $"{{{{{path}}}}}";

        private static XFont CreateFont(
            string? family,
            double size,
            XFontStyleEx style)
        {
            var safeFamily = string.IsNullOrWhiteSpace(family)
                ? "Arial"
                : family;
            try
            {
                return new XFont(
                    safeFamily,
                    size,
                    style,
                    new XPdfFontOptions(PdfFontEncoding.Unicode));
            }
            catch
            {
                return new XFont(
                    "Arial",
                    size,
                    style,
                    new XPdfFontOptions(PdfFontEncoding.Unicode));
            }
        }

        private static bool NeedsWrapping(
            XGraphics gfx,
            string text,
            XFont font,
            XRect rect) =>
            gfx.MeasureString(text, font).Width > rect.Width;

        private static XParagraphAlignment ToParagraphAlignment(
            string? alignment) =>
            alignment?.ToLowerInvariant() switch
            {
                "center" => XParagraphAlignment.Center,
                "right" => XParagraphAlignment.Right,
                "justify" => XParagraphAlignment.Justify,
                _ => XParagraphAlignment.Left
            };

        private static XStringAlignment ToStringAlignment(string? alignment) =>
            alignment?.ToLowerInvariant() switch
            {
                "center" => XStringAlignment.Center,
                "right" => XStringAlignment.Far,
                _ => XStringAlignment.Near
            };

        private static XLineAlignment ToLineAlignment(string? alignment) =>
            alignment?.ToLowerInvariant() switch
            {
                "center" => XLineAlignment.Center,
                "bottom" => XLineAlignment.Far,
                _ => XLineAlignment.Near
            };

        private static XPen CreatePen(ComponentStyle style) =>
            CreatePen(
                style.Border.WidthPt > 0
                    ? style.Border
                    : new BorderStyle
                    {
                        Color = style.Color,
                        WidthPt = 0.5,
                        Style = style.Border.Style
                    });

        private static XPen CreatePen(BorderStyle border)
        {
            var pen = new XPen(
                ParseColor(border.Color),
                border.WidthPt > 0 ? border.WidthPt : 0.5);
            pen.DashStyle = border.Style switch
            {
                "dashed" => XDashStyle.Dash,
                "dotted" => XDashStyle.Dot,
                _ => XDashStyle.Solid
            };
            return pen;
        }

        private static XRect FitImage(
            XRect bounds,
            double sourceWidth,
            double sourceHeight,
            string? fit)
        {
            if (fit == "fill" || sourceWidth <= 0 || sourceHeight <= 0)
            {
                return bounds;
            }

            var scale = fit == "cover"
                ? Math.Max(
                    bounds.Width / sourceWidth,
                    bounds.Height / sourceHeight)
                : Math.Min(
                    bounds.Width / sourceWidth,
                    bounds.Height / sourceHeight);
            var width = sourceWidth * scale;
            var height = sourceHeight * scale;
            return new XRect(
                bounds.X + (bounds.Width - width) / 2,
                bounds.Y + (bounds.Height - height) / 2,
                width,
                height);
        }

        private static byte[]? DecodeImage(string value)
        {
            try
            {
                value = value.Trim();
                var comma = value.IndexOf(',');
                var encoded = value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    ? value[(comma + 1)..]
                    : value;
                return Convert.FromBase64String(encoded);
            }
            catch
            {
                return null;
            }
        }

        private static Position Clone(Position source) =>
            new()
            {
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height,
                Unit = source.Unit
            };

        private static double MmToPt(double mm) =>
            mm * 2.834645669291339;

        private static XColor ParseColor(string? hex)
        {
            if (
                string.IsNullOrWhiteSpace(hex)
                || hex.Equals("transparent", StringComparison.OrdinalIgnoreCase)
            )
            {
                return XColors.Transparent;
            }

            if (hex.StartsWith('#'))
            {
                hex = hex[1..];
            }

            if (hex.Length == 3)
            {
                hex = string.Concat(hex.Select(character => $"{character}{character}"));
            }

            return hex.Length >= 6
                ? XColor.FromArgb(
                    int.Parse(hex[..2], NumberStyles.HexNumber),
                    int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                    int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber))
                : XColors.Black;
        }

        private static string RepairMojibake(string value)
        {
            var result = value
                .Replace("\u00C3\u008D", "Í", StringComparison.Ordinal)
                .Replace("\u00C3\u0093", "Ó", StringComparison.Ordinal)
                .Replace("\u00C3\u00A9", "é", StringComparison.Ordinal)
                .Replace("\u00C3\u00B3", "ó", StringComparison.Ordinal)
                .Replace("\u00C3\u00BA", "ú", StringComparison.Ordinal)
                .Replace("\u00E2\u0080\u0093", "-", StringComparison.Ordinal);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (
                    !result.Contains('Ã')
                    && !result.Contains('â')
                    && !result.Contains('Ă')
                )
                {
                    break;
                }

                try
                {
                    var bytes = Encoding.Latin1.GetBytes(result);
                    var repaired = Encoding.UTF8.GetString(bytes);
                    if (repaired.Count(character => character == '�') > 0)
                    {
                        break;
                    }
                    result = repaired;
                }
                catch
                {
                    break;
                }
            }
            return result;
        }

        private sealed class BindingContext
        {
            private readonly XDocument? _xml;
            private readonly JToken? _json;
            private readonly Dictionary<string, object?> _system;
            private readonly Dictionary<string, object?> _aliases;
            private readonly int _page;
            private readonly int _totalPages;
            private readonly int _recordCount;
            private readonly int _iterationIndex;

            private BindingContext(
                XDocument? xml,
                JToken? json,
                Dictionary<string, object?> system,
                Dictionary<string, object?>? aliases = null,
                int page = 1,
                int totalPages = 1,
                int recordCount = 0,
                int iterationIndex = 0)
            {
                _xml = xml;
                _json = json;
                _system = system;
                _aliases = aliases ?? new(StringComparer.OrdinalIgnoreCase);
                _page = page;
                _totalPages = totalPages;
                _recordCount = recordCount;
                _iterationIndex = iterationIndex;
            }

            public static BindingContext Create(string payload, string contentType, Dictionary<string, string> systemDefaults)
            {
                var system = new Dictionary<string, object?>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var pair in systemDefaults)
                {
                    system[pair.Key] = pair.Value;
                }

                if (!system.ContainsKey("CurrentDateTime"))
                {
                    system["CurrentDateTime"] = DateTimeOffset.Now;
                }
                if (!system.ContainsKey("CurrentYear"))
                {
                    system["CurrentYear"] = DateTime.Now.Year;
                }
                if (!system.ContainsKey("Cufe"))
                {
                    system["Cufe"] = "964e5c464e815616b71f98d41234567890abcdef";
                }
                if (!system.ContainsKey("QrImage"))
                {
                    system["QrImage"] = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                }

                JToken? jsonResult = null;
                XDocument? xmlResult = null;

                if (
                    contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                    && payload.TrimStart().StartsWith('{')
                )
                {
                    var parsed = JToken.Parse(payload);
                    var systemToken = parsed["System"] ?? parsed["system"];
                    if (systemToken is JObject systemObject)
                    {
                        foreach (var property in systemObject.Properties())
                        {
                            system[property.Name] =
                                property.Value.Type == JTokenType.String
                                    ? property.Value.Value<string>()
                                    : property.Value;
                        }
                    }

                    var dataToken = parsed["data"] ?? parsed;
                    if (
                        dataToken.Type == JTokenType.String
                        && dataToken.Value<string>()?.TrimStart().StartsWith('<') == true
                    )
                    {
                        xmlResult = XDocument.Parse(dataToken.Value<string>()!);
                    }
                    else
                    {
                        jsonResult = dataToken;
                    }
                }
                else if (
                    contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                    || payload.TrimStart().StartsWith('<')
                )
                {
                    xmlResult = XDocument.Parse(payload);
                    var root = xmlResult.Root;
                    if (root != null)
                    {
                        var systemElement = root.Elements()
                            .FirstOrDefault(el => el.Name.LocalName.Equals("System", StringComparison.OrdinalIgnoreCase));
                        if (systemElement != null)
                        {
                            foreach (var element in systemElement.Elements())
                            {
                                system[element.Name.LocalName] = element.Value;
                            }
                        }
                    }
                }
                else
                {
                    jsonResult = JToken.Parse(payload);
                }

                return new BindingContext(xmlResult, jsonResult, system);
            }

            public BindingContext WithPage(int page, int totalPages) =>
                new(
                    _xml,
                    _json,
                    _system,
                    new(_aliases, StringComparer.OrdinalIgnoreCase),
                    page,
                    totalPages,
                    _recordCount,
                    _iterationIndex);

            public BindingContext WithAlias(
                string alias,
                object value,
                int iterationIndex)
            {
                var aliases = new Dictionary<string, object?>(
                    _aliases,
                    StringComparer.OrdinalIgnoreCase)
                {
                    [alias] = value
                };
                return new BindingContext(
                    _xml,
                    _json,
                    _system,
                    aliases,
                    _page,
                    _totalPages,
                    _recordCount,
                    iterationIndex);
            }

            public BindingContext WithTableRecordCount(int count) =>
                new(
                    _xml,
                    _json,
                    _system,
                    new(_aliases, StringComparer.OrdinalIgnoreCase),
                    _page,
                    _totalPages,
                    count,
                    _iterationIndex);

            public string Render(string? template)
            {
                if (string.IsNullOrEmpty(template))
                {
                    return string.Empty;
                }

                return BindingRegex.Replace(
                    template,
                    match =>
                    {
                        var expression = match.Groups[1].Value.Trim();
                        var parts = SplitFilters(expression);
                        var value = Resolve(parts[0]);
                        foreach (var filter in parts.Skip(1))
                        {
                            value = ApplyFilter(value, filter);
                        }
                        return ToText(value);
                    });
            }

            public object? Resolve(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                {
                    return null;
                }

                if (segments[0].Equals("System", StringComparison.OrdinalIgnoreCase))
                {
                    return segments.Length > 1
                        ? _system.GetValueOrDefault(segments[1])
                        : null;
                }

                if (segments[0].Equals("Pagina", StringComparison.OrdinalIgnoreCase))
                {
                    return segments.ElementAtOrDefault(1) switch
                    {
                        "Actual" => _page,
                        "Total" => _totalPages,
                        _ => null
                    };
                }

                if (segments[0].Equals("Table", StringComparison.OrdinalIgnoreCase))
                {
                    return segments.ElementAtOrDefault(1) == "RecordCount"
                        ? _recordCount
                        : null;
                }

                if (segments[0].Equals("Iteration", StringComparison.OrdinalIgnoreCase))
                {
                    return segments.ElementAtOrDefault(1) == "Index"
                        ? _iterationIndex
                        : null;
                }

                if (segments[0].Equals("Pending", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolvePending(string.Join('.', segments.Skip(1)));
                }

                if (_aliases.TryGetValue(segments[0], out var alias))
                {
                    return Traverse(alias, segments.Skip(1));
                }

                if (_xml?.Root is { } root)
                {
                    XElement? element = root
                        .Elements()
                        .FirstOrDefault(
                            item => item.Name.LocalName.Equals(
                                segments[0],
                                StringComparison.OrdinalIgnoreCase));
                    foreach (var segment in segments.Skip(1))
                    {
                        element = element?
                            .Elements()
                            .FirstOrDefault(
                                item => item.Name.LocalName.Equals(
                                    segment,
                                    StringComparison.OrdinalIgnoreCase));
                    }
                    return element?.Value;
                }

                return _json?.SelectToken("$." + path);
            }

            public IReadOnlyList<object> Collection(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return Array.Empty<object>();
                }

                if (_xml?.Root is { } root)
                {
                    var elementName = path.Split('.').Last();
                    return root
                        .Elements()
                        .Where(
                            item => item.Name.LocalName.Equals(
                                elementName,
                                StringComparison.OrdinalIgnoreCase))
                        .Cast<object>()
                        .ToList();
                }

                var token = _json?.SelectToken("$." + path);
                return token is JArray array
                    ? array.Cast<object>().ToList()
                    : Array.Empty<object>();
            }

            public bool Evaluate(string? condition)
            {
                if (string.IsNullOrWhiteSpace(condition))
                {
                    return true;
                }

                var comparison = Regex.Match(
                    condition,
                    @"^([\w.]+)\s*(>|>=|<|<=|==|!=)\s*(-?\d+(?:\.\d+)?)$");
                if (
                    comparison.Success
                    && decimal.TryParse(
                        ToText(Resolve(comparison.Groups[1].Value)),
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var left)
                    && decimal.TryParse(
                        comparison.Groups[3].Value,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var right)
                )
                {
                    return comparison.Groups[2].Value switch
                    {
                        ">" => left > right,
                        ">=" => left >= right,
                        "<" => left < right,
                        "<=" => left <= right,
                        "==" => left == right,
                        "!=" => left != right,
                        _ => false
                    };
                }

                return false;
            }

            private object? ResolvePending(string path)
            {
                return path switch
                {
                    "IssuerCheckDigit" => ColombianCheckDigit(
                        ToText(Resolve("Company.StateTaxID"))),
                    "BusinessLine" => "No responsable",
                    "UniqueCodeLabel" => "CUFE:",
                    "AmountInWords" => AmountInWords(
                        ToText(Resolve("InvcHead.DspDocInvoiceAmt"))),
                    "DianValidationDateTime" => _system.GetValueOrDefault("CurrentDateTime"),
                    "ProveedorTecnologico.SitioWeb" => "btw.com.co",
                    "RowVatRate" => 0,
                    "RowLotText" => string.Empty,
                    _ => string.Empty
                };
            }

            private object? ApplyFilter(object? value, string filter)
            {
                var separator = filter.IndexOf(':');
                var name = separator < 0 ? filter : filter[..separator];
                var argument = separator < 0 ? string.Empty : filter[(separator + 1)..];

                return name.Trim().ToLowerInvariant() switch
                {
                    "upper" => ToText(value).ToUpperInvariant(),
                    "date" => FormatDate(value, argument),
                    "number" => FormatNumber(value, argument),
                    "currency" => FormatCurrency(value),
                    "coalescebycurrency" => value,
                    "taxtotal" => 0,
                    _ => value
                };
            }

            private static object? Traverse(
                object? current,
                IEnumerable<string> segments)
            {
                foreach (var segment in segments)
                {
                    current = current switch
                    {
                        XElement element => element
                            .Elements()
                            .FirstOrDefault(
                                child => child.Name.LocalName.Equals(
                                    segment,
                                    StringComparison.OrdinalIgnoreCase)),
                        JToken token => token[segment],
                        _ => null
                    };
                }

                return current is XElement xml ? xml.Value : current;
            }

            private static string[] SplitFilters(string expression) =>
                expression.Split('|', StringSplitOptions.TrimEntries);

            private static string FormatDate(object? value, string format)
            {
                if (
                    DateTimeOffset.TryParse(
                        ToText(value),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out var date)
                )
                {
                    var dotNetFormat = format
                        .Replace("hh", "HH", StringComparison.Ordinal);
                    return date.ToString(
                        string.IsNullOrWhiteSpace(dotNetFormat)
                            ? "dd/MM/yyyy"
                            : dotNetFormat,
                        CultureInfo.InvariantCulture);
                }
                return ToText(value);
            }

            private static string FormatNumber(object? value, string decimals)
            {
                if (
                    decimal.TryParse(
                        ToText(value),
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var number)
                )
                {
                    var precision = int.TryParse(decimals, out var parsed)
                        ? Math.Clamp(parsed, 0, 8)
                        : 2;
                    return number.ToString(
                        $"N{precision}",
                        CultureInfo.InvariantCulture);
                }
                return ToText(value);
            }

            private static string FormatCurrency(object? value)
            {
                if (
                    decimal.TryParse(
                        ToText(value),
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var number)
                )
                {
                    return "$ " + number.ToString("N2", CultureInfo.GetCultureInfo("en-US"));
                }
                return ToText(value);
            }

            private static string ToText(object? value) =>
                value switch
                {
                    null => string.Empty,
                    JValue jsonValue => Convert.ToString(
                        jsonValue.Value,
                        CultureInfo.InvariantCulture) ?? string.Empty,
                    JToken token => token.ToString(),
                    DateTimeOffset date => date.ToString("O"),
                    _ => Convert.ToString(
                        value,
                        CultureInfo.InvariantCulture) ?? string.Empty
                };

            private static string ColombianCheckDigit(string taxId)
            {
                var digits = new string(taxId.Where(char.IsDigit).ToArray());
                if (digits.Length == 0)
                {
                    return string.Empty;
                }

                int[] weights =
                [
                    71, 67, 59, 53, 47, 43, 41, 37, 29, 23, 19, 17, 13, 7, 3
                ];
                var padded = digits.PadLeft(weights.Length, '0');
                var total = padded
                    .Select((digit, index) => (digit - '0') * weights[index])
                    .Sum();
                var remainder = total % 11;
                return (remainder is 0 or 1 ? remainder : 11 - remainder)
                    .ToString(CultureInfo.InvariantCulture);
            }

            private static string AmountInWords(string raw)
            {
                if (
                    !decimal.TryParse(
                        raw,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var amount)
                )
                {
                    return string.Empty;
                }

                var integer = (long)Math.Truncate(amount);
                return $"{SpanishNumber(integer).ToUpperInvariant()} PESOS";
            }

            private static string SpanishNumber(long number)
            {
                if (number == 0) return "cero";
                if (number < 0) return "menos " + SpanishNumber(-number);
                if (number < 30)
                {
                    string[] names =
                    [
                        "", "uno", "dos", "tres", "cuatro", "cinco", "seis",
                        "siete", "ocho", "nueve", "diez", "once", "doce",
                        "trece", "catorce", "quince", "dieciséis", "diecisiete",
                        "dieciocho", "diecinueve", "veinte", "veintiuno",
                        "veintidós", "veintitrés", "veinticuatro", "veinticinco",
                        "veintiséis", "veintisiete", "veintiocho", "veintinueve"
                    ];
                    return names[number];
                }
                if (number < 100)
                {
                    string[] tens =
                    [
                        "", "", "", "treinta", "cuarenta", "cincuenta",
                        "sesenta", "setenta", "ochenta", "noventa"
                    ];
                    return number % 10 == 0
                        ? tens[number / 10]
                        : $"{tens[number / 10]} y {SpanishNumber(number % 10)}";
                }
                if (number == 100) return "cien";
                if (number < 1000)
                {
                    string[] hundreds =
                    [
                        "", "ciento", "doscientos", "trescientos",
                        "cuatrocientos", "quinientos", "seiscientos",
                        "setecientos", "ochocientos", "novecientos"
                    ];
                    return number % 100 == 0
                        ? hundreds[number / 100]
                        : $"{hundreds[number / 100]} {SpanishNumber(number % 100)}";
                }
                if (number < 1_000_000)
                {
                    var thousands = number / 1000;
                    var rest = number % 1000;
                    var prefix = thousands == 1
                        ? "mil"
                        : $"{SpanishNumber(thousands)} mil";
                    return rest == 0
                        ? prefix
                        : $"{prefix} {SpanishNumber(rest)}";
                }
                if (number < 1_000_000_000)
                {
                    var millions = number / 1_000_000;
                    var rest = number % 1_000_000;
                    var prefix = millions == 1
                        ? "un millón"
                        : $"{SpanishNumber(millions)} millones";
                    return rest == 0
                        ? prefix
                        : $"{prefix} {SpanishNumber(rest)}";
                }
                return number.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
