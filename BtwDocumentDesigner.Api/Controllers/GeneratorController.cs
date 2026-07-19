namespace BtwDocumentDesigner.Api.Controllers
{
    [ApiController]
    [Route("DocumentDesignerApi/pdf")]
    public class GeneratorController : ControllerBase
    {
        private readonly IGeneratorService _generatorService;

        public GeneratorController(IGeneratorService generatorService)
        {
            _generatorService = generatorService;
        }

        [SwaggerOperation(Summary = "Genera un PDF", Description = "Recibe el nombre y versión del diseño, junto con un payload XML/JSON, y devuelve el archivo PDF generado.")]
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
                return BadRequest($"Error generando el PDF: {ex.Message}");
            }
        }

        [SwaggerOperation(
            Summary = "Genera un PDF con contrato 3.0",
            Description = "Recibe payload XML/JSON y parámetros runtime tipados en un único envelope.")]
        [HttpPost("generate-v3")]
        [Consumes("application/json")]
        public async Task<IActionResult> GenerateV3([FromBody] GeneratePdfRequest request)
        {
            try
            {
                var pdfBytes = await _generatorService.GeneratePdfAsync(
                    request.DesignName,
                    request.Version,
                    request.Payload,
                    request.PayloadType,
                    request.Runtime);
                return File(
                    pdfBytes,
                    "application/pdf",
                    $"Generated_{DateTime.UtcNow.Ticks}.pdf");
            }
            catch (ContractValidationException ex)
            {
                return BadRequest(new
                {
                    Error = "Contrato de diseño inválido.",
                    Details = ex.Errors
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    public sealed class GeneratePdfRequest
    {
        public string DesignName { get; set; } = string.Empty;
        public int Version { get; set; }
        public string PayloadType { get; set; } = "application/xml";
        public string Payload { get; set; } = string.Empty;
        public Dictionary<string, System.Text.Json.JsonElement> Runtime { get; set; } = [];
    }
}



