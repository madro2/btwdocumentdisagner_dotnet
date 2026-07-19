namespace BtwDocumentDesigner.Application.Interfaces
{
    public interface IGeneratorService
    {
        Task<byte[]> GeneratePdfAsync(
            string designName,
            int version,
            string payload,
            string contentType,
            IReadOnlyDictionary<string, System.Text.Json.JsonElement>? runtime = null);
    }
}
