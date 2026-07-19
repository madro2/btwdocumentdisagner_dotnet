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

        [SwaggerOperation(Summary = "Generar un PDF", Description = "Genera un PDF a partir de una plantilla y los datos de una factura o documento.")]
        [HttpPost("generate")]
        [Consumes("application/xml", "application/json", "text/plain")]
        public async Task<IActionResult> Generate(
            [FromQuery, Display(Name = "Nombre de la plantilla")] string designName,
            [FromQuery, Display(Name = "Versión de la plantilla")] int version,
            [FromBody, Display(Name = "Datos del documento")] string payload)
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
            Summary = "Generar un PDF con datos adicionales",
            Description = "Genera un PDF usando una plantilla, los datos XML o JSON y valores adicionales como CUFE o código QR.")]
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
        [Display(Name = "Nombre de la plantilla", Description = "Nombre de la plantilla que se usará para generar el PDF.")]
        public string DesignName { get; set; } = string.Empty;

        [Display(Name = "Versión de la plantilla", Description = "Número de versión de la plantilla seleccionada.")]
        public int Version { get; set; }

        [Display(Name = "Tipo de datos", Description = "Formato de los datos recibidos: application/xml o application/json.")]
        public string PayloadType { get; set; } = "application/xml";

        [Display(Name = "Datos del documento", Description = "Contenido XML o JSON con la información que aparecerá en el PDF.")]
        public string Payload { get; set; } = string.Empty;

        [Display(Name = "Datos adicionales", Description = "Valores complementarios como CUFE, código QR, fecha de validación o año actual.")]
        public Dictionary<string, System.Text.Json.JsonElement> Runtime { get; set; } = [];
    }
}



