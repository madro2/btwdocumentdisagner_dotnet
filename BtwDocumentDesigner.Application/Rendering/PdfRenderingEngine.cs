using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace BtwDocumentDesigner.Application.Rendering
{
    public class PdfRenderingEngine
    {
        private readonly IImageRepository _imageRepository;

        public PdfRenderingEngine(IImageRepository imageRepository)
        {
            _imageRepository = imageRepository;
        }

        public async Task<byte[]> GeneratePdfAsync(string jsonConfig, string payload, string contentType)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var schema = JsonSerializer.Deserialize<PdfDesignSchema>(jsonConfig, options);

            if (schema == null) throw new Exception("Diseño JSON inválido.");

            XDocument? xmlData = null;
            JObject? jsonData = null;

            if (contentType.Contains("xml") || payload.TrimStart().StartsWith("<"))
            {
                xmlData = XDocument.Parse(payload);
            }
            else
            {
                jsonData = JObject.Parse(payload);
            }

            using var document = new PdfDocument();
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            
            using var gfx = XGraphics.FromPdfPage(page);

            await RenderComponents(gfx, schema.Components, xmlData, jsonData);

            using var ms = new MemoryStream();
            document.Save(ms, false);
            return ms.ToArray();
        }

        private async Task RenderComponents(XGraphics gfx, List<PdfComponent> components, XDocument? xmlData, JObject? jsonData, double parentX = 0, double parentY = 0)
        {
            foreach (var comp in components)
            {
                double x = parentX + MmToPt(comp.Position.X);
                double y = parentY + MmToPt(comp.Position.Y);
                double width = MmToPt(comp.Position.Width);
                double height = MmToPt(comp.Position.Height);

                var state = gfx.Save();
                
                if (comp.RotationDegrees.HasValue && comp.RotationDegrees.Value != 0)
                {
                    gfx.RotateAtTransform(comp.RotationDegrees.Value, new XPoint(x, y));
                }

                if (comp.Type == "text")
                {
                    string text = ResolveDataBinding(comp.Content.Value, xmlData, jsonData);
                    var fontStyle = comp.Style.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular;
                    var font = new XFont(comp.Style.FontFamily, comp.Style.FontSizePt, fontStyle);
                    var brush = new XSolidBrush(ParseColor(comp.Style.Color));
                    var format = new XStringFormat();
                    
                    if (comp.Style.Alignment == "center")
                    {
                        format.Alignment = XStringAlignment.Center;
                        format.LineAlignment = XLineAlignment.Center;
                    }
                    else if (comp.Style.Alignment == "right")
                    {
                        format.Alignment = XStringAlignment.Far;
                    }

                    gfx.DrawString(text, font, brush, new XRect(x, y, width, height), format);
                }
                else if (comp.Type == "image" && !string.IsNullOrEmpty(comp.Content.AssetId))
                {
                    if (Guid.TryParse(comp.Content.AssetId, out Guid assetId))
                    {
                        var imageData = await _imageRepository.GetImageDataAsync(assetId);
                        if (imageData != null)
                        {
                            using var imgStream = new MemoryStream(imageData);
                            using var xImage = XImage.FromStream(imgStream);
                            gfx.DrawImage(xImage, x, y, width, height);
                        }
                    }
                }
                else if (comp.Type == "container")
                {
                    if (comp.Components != null && comp.Components.Any())
                    {
                        await RenderComponents(gfx, comp.Components, xmlData, jsonData, x, y);
                    }
                }

                gfx.Restore(state);
            }
        }

        private string ResolveDataBinding(string template, XDocument? xmlData, JObject? jsonData)
        {
            if (string.IsNullOrEmpty(template)) return "";

            var regex = new Regex(@"\{\{(.+?)\}\}");
            return regex.Replace(template, match =>
            {
                string path = match.Groups[1].Value.Trim();
                try
                {
                    if (xmlData != null)
                    {
                        string xpath = "//" + path.Replace(".", "/");
                        var element = xmlData.XPathSelectElement(xpath);
                        return element?.Value ?? match.Value;
                    }
                    else if (jsonData != null)
                    {
                        string jsonPath = "$." + path;
                        var token = jsonData.SelectToken(jsonPath);
                        return token?.ToString() ?? match.Value;
                    }
                }
                catch { }
                return match.Value;
            });
        }

        private double MmToPt(double mm) => mm * 2.834645669291339;
        
        private XColor ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || !hex.StartsWith("#") || hex.Length < 7) return XColors.Black;
            return XColor.FromArgb(
                int.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber)
            );
        }
    }
}
