namespace BtwDocumentDesigner.Application.Services;

public sealed class GeneratorService : IGeneratorService
{
    private readonly IDesignRepository _designRepository;
    private readonly PdfRenderingEngine _engine;

    public GeneratorService(
        IDesignRepository designRepository,
        PdfRenderingEngine engine)
    {
        _designRepository = designRepository;
        _engine = engine;
    }

    public async Task<byte[]> GeneratePdfAsync(
        string designName,
        int version,
        string payload,
        string contentType,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement>? runtime = null)
    {
        var design = await _designRepository.SearchAsync(designName, version);
        if (design is null)
            throw new InvalidOperationException("Plantilla de diseño no encontrada.");

        return await _engine.GeneratePdfAsync(
            design.JsonConfiguration,
            payload,
            contentType,
            runtime);
    }
}
