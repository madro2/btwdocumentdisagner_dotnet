namespace BtwDocumentDesigner.Application.Rendering
{
    public class PdfDesignSchema
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public PageSettings Page { get; set; } = new();
        public List<PdfComponent> Components { get; set; } = new();
    }

    public class PageSettings
    {
        public string Size { get; set; } = "A4";
        public string Orientation { get; set; } = "portrait";
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public Margins MarginsMm { get; set; } = new();
    }

    public class Margins
    {
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }
        public double Left { get; set; }
    }

    public class PdfComponent
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // text, image, container
        public Position Position { get; set; } = new();
        public double? RotationDegrees { get; set; }
        public ComponentContent Content { get; set; } = new();
        public ComponentStyle Style { get; set; } = new();
        public List<PdfComponent> Components { get; set; } = new(); // For containers
    }

    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Unit { get; set; } = "mm";
    }

    public class ComponentContent
    {
        public string Value { get; set; } = string.Empty;
        public string AssetId { get; set; } = string.Empty; // For images
    }

    public class ComponentStyle
    {
        public string FontFamily { get; set; } = "Arial";
        public double FontSizePt { get; set; } = 10;
        public bool Bold { get; set; }
        public string Alignment { get; set; } = "left"; // left, center, right
        public string Color { get; set; } = "#000000";
    }
}
