namespace BtwDocumentDesigner.Application.Interfaces
{
    public interface IGeneratorService
    {
        Task<byte[]> GeneratePdfAsync(string designName, int version, string payload, string contentType);
    }
}
