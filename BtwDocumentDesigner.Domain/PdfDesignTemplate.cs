namespace BtwDocumentDesigner.Domain
{
    public class PdfDesignTemplate
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocumentType { get; set; } = string.Empty;
        public string DesignName { get; set; } = string.Empty;
        public int DesignVersion { get; set; }
        public string JsonConfiguration { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModificationDate { get; set; }
        public string? ModificationUser { get; set; }
    }
}
