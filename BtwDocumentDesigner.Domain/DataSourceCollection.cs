namespace BtwDocumentDesigner.Domain
{
    public sealed class DataSourceCollection
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SourceType { get; set; } = "XML/JSON";
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModificationDate { get; set; }
        public ICollection<DataSourceField> Fields { get; set; } = [];
    }
}
