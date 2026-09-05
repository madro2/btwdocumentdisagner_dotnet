namespace BtwDocumentDesigner.Application.Services
{
    public class GeneratorService : IGeneratorService
    {
        private readonly IDesignRepository _designRepository;
        private readonly PdfRenderingEngine _engine;

        public GeneratorService(IDesignRepository designRepository, PdfRenderingEngine engine)
        {
            _designRepository = designRepository;
            _engine = engine;
        }

        public async Task<byte[]> GeneratePdfAsync(string designName, int version, string payload, string contentType)
        {
            var design = await _designRepository.SearchAsync(designName, version);
            if (design == null) throw new InvalidOperationException("Plantilla de diseño no encontrada.");

            return await _engine.GeneratePdfAsync(design.JsonConfiguration, payload, contentType);
        }

        public async Task<byte[]> GeneratePdfDirectAsync(string jsonConfiguration, string payload, string contentType)
        {
            if (string.IsNullOrWhiteSpace(jsonConfiguration))
            {
                throw new ArgumentException("La configuración del diseño no puede estar vacía.", nameof(jsonConfiguration));
            }

            return await _engine.GeneratePdfAsync(jsonConfiguration, payload, contentType);
        }
    }
}
