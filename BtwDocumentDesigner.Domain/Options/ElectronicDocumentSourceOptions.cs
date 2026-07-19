namespace BtwDocumentDesigner.Domain.Options
{
    public sealed class ElectronicDocumentSourceOptions
    {
        public const string SectionName = "ElectronicDocuments";

        public string BaseUrl { get; set; } = string.Empty;
        public string XmlErpPathTemplate { get; set; } =
            "filesfe/FilesFE/{cufe}/XMLERP/WithPath";
    }
}
