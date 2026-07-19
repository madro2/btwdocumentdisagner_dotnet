namespace BtwDocumentDesigner.Domain
{
    public sealed class DataSourceField
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DataSourceCollectionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string DataType { get; set; } = "String";
        public string Cardinality { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public DataSourceCollection? Collection { get; set; }
    }
}
