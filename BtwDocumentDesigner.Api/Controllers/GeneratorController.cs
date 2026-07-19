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
    }
}



