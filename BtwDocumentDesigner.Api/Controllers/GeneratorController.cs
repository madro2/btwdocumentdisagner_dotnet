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
                return BadRequest($"Error generando el PDF: {ex.Message}");
            }
        }
    }
}



