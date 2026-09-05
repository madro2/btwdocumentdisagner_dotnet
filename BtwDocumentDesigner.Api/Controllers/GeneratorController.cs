namespace BtwDocumentDesigner.Api.Controllers
{
    [ApiController]
    [Route("DocumentDesignerApi/pdf")]
    public class GeneratorController : ControllerBase
    {
        private readonly IGeneratorService _generatorService;
        private readonly ILogger<GeneratorController> _logger;

        public GeneratorController(
            IGeneratorService generatorService,
            ILogger<GeneratorController> logger)
        {
            _generatorService = generatorService;
            _logger = logger;
        }

        [SwaggerOperation(Summary = "Genera un PDF", Description = "Recibe el nombre y versión del diseño, junto con un payload JSON, y devuelve el archivo PDF generado.")]
        [HttpPost("generate")]
        [Consumes("application/xml", "application/json", "text/plain")]
        public async Task<IActionResult> Generate([FromQuery] string designName, [FromQuery] int version, [FromBody] string payload)
        {
            try
            {
                string contentType = Request.ContentType ?? "application/json";

                var pdfBytes = await _generatorService.GeneratePdfAsync(designName, version, payload, contentType);
                
                return File(pdfBytes, "application/pdf", $"Generated_{DateTime.UtcNow.Ticks}.pdf");
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generando el PDF para el diseño {DesignName}, versión {Version}.",
                    designName,
                    version);
                return BadRequest($"Error generando el PDF: {ex.Message}");
            }
        }

        [SwaggerOperation(Summary = "Genera un PDF directo desde la plantilla JSON (incluso incompleta o en borrador)", Description = "Recibe el contrato del diseño y el payload, y devuelve el archivo PDF generado sin requerir que el diseño esté previamente guardado.")]
        [HttpPost("preview-direct")]
        public async Task<IActionResult> PreviewDirect([FromBody] DirectPdfPreviewRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.DesignJson))
                {
                    return BadRequest("La plantilla JSON (designJson) es obligatoria.");
                }

                string payload = string.IsNullOrWhiteSpace(request.Payload) ? "{}" : request.Payload;
                string contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/json" : request.ContentType;

                var pdfBytes = await _generatorService.GeneratePdfDirectAsync(request.DesignJson, payload, contentType);
                return File(pdfBytes, "application/pdf", $"Preview_{DateTime.UtcNow.Ticks}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando la vista previa del PDF directo.");
                return BadRequest($"Error generando el PDF: {ex.Message}");
            }
        }
    }

    public sealed class DirectPdfPreviewRequest
    {
        public string DesignJson { get; set; } = string.Empty;
        public string? Payload { get; set; }
        public string? ContentType { get; set; }
    }
}



