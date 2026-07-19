namespace BtwDocumentDesigner.Domain
{
    public sealed class SystemDefaultValue
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
