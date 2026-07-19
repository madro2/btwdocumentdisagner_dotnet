using System.Text.Json;
using System.Text.Json.Serialization;

namespace BtwDocumentDesigner.Application.Rendering;

public sealed class PdfDesignSchema
{
    public string SchemaVersion { get; set; } = string.Empty;
    public DocumentSettings Document { get; set; } = new();
    public DataSourceSettings DataSource { get; set; } = new();
    public PageSettings Page { get; set; } = new();
    public List<ResourceDefinition> Resources { get; set; } = [];
    public Dictionary<string, ComponentStyle> SharedStyles { get; set; } = [];
    public List<PdfComponent> Components { get; set; } = [];
    public RenderingRuleSettings RenderingRules { get; set; } = new();
    public ContractValidationSettings Validation { get; set; } = new();
}

public sealed class DocumentSettings
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? Description { get; set; }
}

public sealed class DataSourceSettings
{
    public string Type { get; set; } = "xml";
    public string RootPath { get; set; } = "/NewDataSet";
    public string PathDialect { get; set; } = "dotPath";
    public string SelectionMode { get; set; } = "directChildren";
    public bool AllowMissingFields { get; set; }
    public List<RuntimeParameterDefinition> RuntimeParameters { get; set; } = [];
    public List<DataTableDefinition> Tables { get; set; } = [];
    public Dictionary<string, Dictionary<string, string>> Lookups { get; set; } = [];
    public List<ComputedFieldDefinition> ComputedFields { get; set; } = [];
}

public sealed class RuntimeParameterDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public JsonElement? DefaultValue { get; set; }
}

public sealed class DataTableDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Cardinality { get; set; } = "zeroOrOne";
    public string DataPath { get; set; } = string.Empty;
    public ContentSelector? Selector { get; set; }
}

public sealed class ComputedFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public ResolverDefinition Resolver { get; set; } = new();
}

public sealed class ResolverDefinition
{
    public string Kind { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Lookup { get; set; }
    public BindingOperand? Key { get; set; }
    public List<BindingOperand> Arguments { get; set; } = [];
    public JsonElement? DefaultValue { get; set; }
}

public sealed class BindingOperand
{
    public string Kind { get; set; } = string.Empty;
    public string? Path { get; set; }
    public JsonElement? Value { get; set; }
}

public sealed class PageSettings
{
    public string Size { get; set; } = "A4";
    public string Orientation { get; set; } = "portrait";
    public double WidthMm { get; set; } = 210;
    public double HeightMm { get; set; } = 297;
    public Margins MarginsMm { get; set; } = new();
    public string Background { get; set; } = "#FFFFFF";
}

public sealed class Margins
{
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Left { get; set; }
}

public sealed class ResourceDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Source { get; set; }
    public string? Data { get; set; }
    public bool Required { get; set; }
}

public sealed class PdfComponent
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool Required { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public int ZIndex { get; set; }
    public string? StyleRef { get; set; }
    public Position Position { get; set; } = new();
    public double? RotationDegrees { get; set; }
    public ComponentContent Content { get; set; } = new();
    public ComponentStyle Style { get; set; } = new();
    public ComponentBehavior Behavior { get; set; } = new();
    public ComponentAnchor? Anchor { get; set; }
    public List<TableColumn> Columns { get; set; } = [];
    public List<TableColumn> OptionalColumns { get; set; } = [];
    public List<PdfComponent> Components { get; set; } = [];
    public Dictionary<string, JsonElement> Properties { get; set; } = [];
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
    public List<string> DataPaths { get; set; } = [];
    public JsonElement? DefaultValue { get; set; }
    public string? Url { get; set; }
    public string? Source { get; set; }
    public string? AssetId { get; set; }
    public string? Fit { get; set; }
    public string? Mode { get; set; }
    public string? CollectionPath { get; set; }
    public string? RowAlias { get; set; }
    public ContentSelector? Selector { get; set; }
    public JsonElement? Rows { get; set; }
    public List<TableRow> OptionalRows { get; set; } = [];
    public List<TableField> Fields { get; set; } = [];
    public bool ShowRecordCount { get; set; }
    public string? RecordCountLabel { get; set; }
    public string? Format { get; set; }
    public Dictionary<string, BindingDefinition> Bindings { get; set; } = [];
}

public sealed class ContentSelector
{
    public string? Type { get; set; }
    public string? ParentPath { get; set; }
    public string? ElementName { get; set; }
    public string? Path { get; set; }
}

public sealed class BindingDefinition
{
    public string? Source { get; set; }
    public string? DataPath { get; set; }
    public string? Function { get; set; }
    public List<BindingOperand> Arguments { get; set; } = [];
    public JsonElement? DefaultValue { get; set; }
}

public sealed class TableColumn
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public double? WidthMm { get; set; }
    public string? Value { get; set; }
    public string? DataPath { get; set; }
    public List<string> DataPaths { get; set; } = [];
    public string? DefaultValue { get; set; }
    public FallbackDefinition? Fallback { get; set; }
    public string? Alignment { get; set; }
    public string? VisibilityCondition { get; set; }
    public ComponentStyle Style { get; set; } = new();
}

public sealed class FallbackDefinition
{
    public string? DataPath { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class TableRow
{
    public string? Label { get; set; }
    public string? Value { get; set; }
    public string? DataPath { get; set; }
    public List<string> DataPaths { get; set; } = [];
    public string? DefaultValue { get; set; }
    public string? CollectionPath { get; set; }
    public string? VisibilityCondition { get; set; }
}

public sealed class TableField
{
    public string Column { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? DataPath { get; set; }
    public List<string> DataPaths { get; set; } = [];
    public string? DefaultValue { get; set; }
}

public sealed class ComponentStyle
{
    public string? FontFamily { get; set; }
    public double? FontSizePt { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public string? Alignment { get; set; }
    public string? VerticalAlignment { get; set; }
    public string? Color { get; set; }
    public string? Background { get; set; }
    public double? LineHeight { get; set; }
    public double? Padding { get; set; }
    public BorderStyleDefinition? Border { get; set; }
    public double? WidthPt { get; set; }
    public string? Style { get; set; }
    public double? RowHeightMm { get; set; }
    public string? Fit { get; set; }
    public bool? HeaderBold { get; set; }
    public HeaderStyleDefinition? Header { get; set; }

    public ComponentStyle Merge(ComponentStyle overlay) => new()
    {
        FontFamily = overlay.FontFamily ?? FontFamily,
        FontSizePt = overlay.FontSizePt ?? FontSizePt,
        Bold = overlay.Bold ?? Bold,
        Italic = overlay.Italic ?? Italic,
        Underline = overlay.Underline ?? Underline,
        Alignment = overlay.Alignment ?? Alignment,
        VerticalAlignment = overlay.VerticalAlignment ?? VerticalAlignment,
        Color = overlay.Color ?? Color,
        Background = overlay.Background ?? Background,
        LineHeight = overlay.LineHeight ?? LineHeight,
        Padding = overlay.Padding ?? Padding,
        Border = overlay.Border ?? Border,
        WidthPt = overlay.WidthPt ?? WidthPt,
        Style = overlay.Style ?? Style,
        RowHeightMm = overlay.RowHeightMm ?? RowHeightMm,
        Fit = overlay.Fit ?? Fit,
        HeaderBold = overlay.HeaderBold ?? HeaderBold,
        Header = overlay.Header ?? Header
    };
}

public sealed class HeaderStyleDefinition
{
    public bool? Bold { get; set; }
    public string? Alignment { get; set; }
    public string? Background { get; set; }
}

public sealed class BorderStyleDefinition
{
    public string? Color { get; set; }
    public double? WidthPt { get; set; }
    public string? Style { get; set; }
    public double? RadiusMm { get; set; }
}

public sealed class ComponentBehavior
{
    public string? Mode { get; set; }
    public string? RepeatOn { get; set; }
    public bool? AllowOverflow { get; set; }
    public double? HeaderHeightMm { get; set; }
    public double? MinRowHeightMm { get; set; }
    public double? MaxRowHeightMm { get; set; }
    public bool? RepeatHeader { get; set; }
    public bool? AllowPageBreak { get; set; }
    public bool? KeepRowTogether { get; set; }
    public bool? GrowVertically { get; set; }
    public bool? MoveFollowingComponents { get; set; }
    public bool? GenerateRuntimeComponents { get; set; }
    public bool? PersistRuntimeComponents { get; set; }
    public double? ReservedBottomSpaceMm { get; set; }
    public string? Placement { get; set; }
    public bool? ReserveSpace { get; set; }
}

public sealed class ComponentAnchor
{
    public string Mode { get; set; } = "absolute";
    public string? ComponentId { get; set; }
    public double SpacingMm { get; set; }
}

public sealed class RenderingRuleSettings
{
    public string BaseUnit { get; set; } = "mm";
    public string CoordinateOrigin { get; set; } = "topLeft";
    public string UnsupportedComponentPolicy { get; set; } = "error";
}

public sealed class ContractValidationSettings
{
    public string? Status { get; set; }
    public List<PendingBindingDefinition> PendingBindings { get; set; } = [];
}

public sealed class PendingBindingDefinition
{
    public string Id { get; set; } = string.Empty;
    public string? DataPath { get; set; }
    public string Status { get; set; } = "pendingValidation";
}
