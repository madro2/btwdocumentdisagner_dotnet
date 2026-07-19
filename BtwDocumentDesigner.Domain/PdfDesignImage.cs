namespace BtwDocumentDesigner.Domain
{
    public class PdfDesignImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileName { get; set; } = string.Empty;
        public string? ResourceKey { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    }
}
