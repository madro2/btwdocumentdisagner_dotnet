namespace BtwDocumentDesigner.Api.Controllers
{
    [ApiController]
    [Route("DocumentDesignerApi/electronic-documents")]
    public sealed class ElectronicDocumentsController : ControllerBase
    {
        private readonly IElectronicDocumentService _service;

        public ElectronicDocumentsController(IElectronicDocumentService service)
        {
            _service = service;
        }

        [HttpGet("{cufe}/source-url")]
        public ActionResult<ElectronicDocumentSourceResponse> GetSourceUrl(
            string cufe)
        {
            try
            {
                return Ok(
                    new ElectronicDocumentSourceResponse(
                        cufe,
                        _service.GetSourceUri(cufe).AbsoluteUri));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception.Message);
            }
        }

        [HttpGet("{cufe}/xml")]
        public async Task<ActionResult<ElectronicDocumentXmlResponse>> DownloadXml(
            string cufe,
            CancellationToken cancellationToken)
        {
            try
            {
                var document = await _service.DownloadXmlAsync(
                    cufe,
                    cancellationToken);
                return Ok(
                    new ElectronicDocumentXmlResponse(
                        document.Cufe,
                        document.FileName,
                        "application/xml; charset=utf-8",
                        document.Base64));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception.Message);
            }
            catch (TaskCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(
                    StatusCodes.Status504GatewayTimeout,
                    "El servicio de documentos electrónicos no respondió dentro del tiempo esperado.");
            }
            catch (HttpRequestException exception)
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    $"No fue posible descargar el XML ERP: {exception.Message}");
            }
        }

        [HttpPost("interpret")]
        public async Task<IActionResult> Interpret(
            [FromBody] InterpretElectronicDocumentRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var pdf = await _service.GeneratePdfAsync(
                    request.Cufe,
                    request.DesignName,
                    request.Version,
                    cancellationToken);
                return File(
                    pdf,
                    "application/pdf",
                    $"{request.Cufe}.pdf");
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return NotFound(exception.Message);
            }
            catch (TaskCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(
                    StatusCodes.Status504GatewayTimeout,
                    "El servicio de documentos electrónicos no respondió dentro del tiempo esperado.");
            }
            catch (HttpRequestException exception)
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    $"No fue posible consultar el documento electrónico: {exception.Message}");
            }
        }
    }

    public sealed record InterpretElectronicDocumentRequest(
        string Cufe,
        string DesignName,
        int Version);

    public sealed record ElectronicDocumentXmlResponse(
        string Cufe,
        string FileName,
        string ContentType,
        string Base64);

    public sealed record ElectronicDocumentSourceResponse(
        string Cufe,
        string Url);
}
