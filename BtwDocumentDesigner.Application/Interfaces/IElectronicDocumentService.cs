namespace BtwDocumentDesigner.Application.Interfaces
{
    public interface IElectronicDocumentService
    {
        Task<ElectronicDocumentXml> DownloadXmlAsync(
            string cufe,
            CancellationToken cancellationToken = default);

        Task<byte[]> GeneratePdfAsync(
            string cufe,
            string designName,
            int version,
            CancellationToken cancellationToken = default);
    }

    public sealed record ElectronicDocumentXml(
        string Cufe,
        string FileName,
        string Xml,
        string Base64);
}
