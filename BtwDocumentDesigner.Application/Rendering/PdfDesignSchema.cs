using System.Text.Json;

namespace BtwDocumentDesigner.Application.Rendering
{
    public sealed class PdfDesignSchema
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public PageSettings Page { get; set; } = new();
        public List<PdfComponent> Components { get; set; } = new();
        /// <summary>
        /// Páginas del diseño. Si está vacío, se usa <see cref="Page"/> y
        /// <see cref="Components"/> de la raíz (compatibilidad con contratos de una página).
        /// </summary>
        public List<PdfDesignPage> Pages { get; set; } = new();
    }

    public sealed class PdfDesignPage
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public PageSettings Page { get; set; } = new();
        public List<PdfComponent> Components { get; set; } = new();
    }

    public sealed class PageSettings
    {
        public string Size { get; set; } = "A4";
        public string Orientation { get; set; } = "portrait";
        public double WidthMm { get; set; } = 210;
        public double HeightMm { get; set; } = 297;
        public string Background { get; set; } = "#FFFFFF";
        public Margins MarginsMm { get; set; } = new();
    }

    public sealed class Margins
    {
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }
        public double Left { get; set; }
    }

    public sealed class PdfComponent
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Position Position { get; set; } = new();
        public double? RotationDegrees { get; set; }
        public ComponentContent Content { get; set; } = new();
        public ComponentStyle Style { get; set; } = new();
        public ComponentBehavior Behavior { get; set; } = new();
        public List<PdfComponent> Components { get; set; } = new();
        public List<TableColumn> Columns { get; set; } = new();
        public List<TableColumn> OptionalColumns { get; set; } = new();
        public RowTemplate RowTemplate { get; set; } = new();
        public AnchorDefinition? Anchor { get; set; }
        public JsonElement Properties { get; set; }
    }

    public sealed class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Unit { get; set; } = "mm";
    }

    public sealed class ComponentContent
    {
        public string? Value { get; set; }
        public string? DataPath { get; set; }
        public List<string> DataPaths { get; set; } = new();
        public string? DefaultValue { get; set; }
        public string? Source { get; set; }
        public string? AssetId { get; set; }
        public string? Fit { get; set; }
        public string? Mode { get; set; }
        public string? Format { get; set; }
        public string? RowAlias { get; set; }
        public bool ShowRecordCount { get; set; }
        public string? RecordCountLabel { get; set; }
        public List<TableRow> Rows { get; set; } = new();
        public List<TableRow> OptionalRows { get; set; } = new();
        public List<TableField> Fields { get; set; } = new();
    }

    public sealed class TableRow
    {
        public string? Label { get; set; }
        public string? Value { get; set; }
        public string? DataPath { get; set; }
        public string? DefaultValue { get; set; }
        public string? CollectionPath { get; set; }
        public string? VisibilityCondition { get; set; }
    }

    public sealed class TableField
    {
        public string Column { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? DataPath { get; set; }
        public string? DefaultValue { get; set; }
    }

    public sealed class TableColumn
    {
        public string Id { get; set; } = string.Empty;
        public string? Title { get; set; }
        public double WidthMm { get; set; }
        public string? Value { get; set; }
        public string? DataPath { get; set; }
        public string? DefaultValue { get; set; }
        public string? Alignment { get; set; }
        public ComponentStyle Style { get; set; } = new();
        public FallbackDefinition? Fallback { get; set; }
        
        // Personalización de columna de encabezado
        public string? HeaderBackground { get; set; }
        public string? HeaderColor { get; set; }
        public string? HeaderAlignment { get; set; }
        public bool? HeaderBold { get; set; }
        public bool? HeaderItalic { get; set; }
        public double? HeaderFontSizePt { get; set; }
    }

    public sealed class FallbackDefinition
    {
        public string? DataPath { get; set; }
    }

    public sealed class RowTemplate
    {
        public double MinHeightMm { get; set; } = 6;
        public double MaxHeightMm { get; set; } = 18;
    }

    public sealed class AnchorDefinition
    {
        public string? Mode { get; set; }
        public string? ComponentId { get; set; }
        public double SpacingMm { get; set; }
    }

    public sealed class ComponentBehavior
    {
        public string? Mode { get; set; }
        public string? RepeatOn { get; set; }
        public bool RepeatHeader { get; set; }
        public bool AllowPageBreak { get; set; }
        public bool GrowVertically { get; set; }
        public bool MoveFollowingComponents { get; set; }
        public double HeaderHeightMm { get; set; }
        public double MinRowHeightMm { get; set; }
        public double MaxRowHeightMm { get; set; }
        public double ReservedBottomSpaceMm { get; set; }
    }

    public sealed class ComponentStyle
    {
        public string FontFamily { get; set; } = "Arial";
        public double FontSizePt { get; set; } = 10;
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public string Alignment { get; set; } = "left";
        public string VerticalAlignment { get; set; } = "top";
        public string Color { get; set; } = "#000000";
        public string? Background { get; set; }
        public double Padding { get; set; }
        public double RowHeightMm { get; set; }
        public double LineHeight { get; set; } = 1.1;
        public string? Fit { get; set; }
        public bool HeaderBold { get; set; }
        public BorderStyle Border { get; set; } = new();
        public HeaderStyle Header { get; set; } = new();
        public string? BorderPreset { get; set; } = "all";
        public string? AlternateRowBackground { get; set; }
        public double HeaderHeightMm { get; set; }
        public double CellPaddingMm { get; set; }
    }

    public sealed class HeaderStyle
    {
        public bool Bold { get; set; }
        public string Alignment { get; set; } = "center";
        public string? Background { get; set; }
    }

    public sealed class BorderStyle
    {
        public string Color { get; set; } = "#000000";
        public double WidthPt { get; set; } = 0;
        public string Style { get; set; } = "none";
        public double RadiusMm { get; set; }
    }
}
