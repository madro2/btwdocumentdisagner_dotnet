namespace BtwDocumentDesigner.Api.Controllers
{
    [ApiController]
    [Route("DocumentDesignerApi/[controller]")]
    public class DesignsController : ControllerBase
    {
        private readonly IDesignService _designService;

        public DesignsController(IDesignService designService)
        {
            _designService = designService;
        }

        [SwaggerOperation(Summary = "Crear una plantilla", Description = "Guarda una nueva plantilla para generar documentos PDF.")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PdfDesignTemplate design)
        {
            try
            {
                var created = await _designService.CreateDesignAsync(design);
                return Created($"/DocumentDesignerApi/Designs/{created.Id}", created);
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
                return Conflict(ex.Message);
            }
        }

        [SwaggerOperation(Summary = "Consultar plantillas", Description = "Muestra todas las plantillas de documentos disponibles.")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _designService.GetAllDesignsAsync());
        }

        [SwaggerOperation(Summary = "Consultar una plantilla", Description = "Busca una plantilla usando su identificador único.")]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var design = await _designService.GetDesignByIdAsync(id);
            return design != null ? Ok(design) : NotFound("Diseño no encontrado.");
        }

        [SwaggerOperation(Summary = "Buscar una plantilla", Description = "Busca una plantilla por su nombre y número de versión.")]
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery, Display(Name = "Nombre de la plantilla")] string name,
            [FromQuery, Display(Name = "Versión de la plantilla")] int version)
        {
            var design = await _designService.SearchDesignAsync(name, version);
            return design != null ? Ok(new { Id = design.Id }) : NotFound("Diseño no encontrado.");
        }

        [SwaggerOperation(Summary = "Actualizar una plantilla", Description = "Actualiza la información y la configuración de una plantilla existente.")]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PdfDesignTemplate updateDesign)
        {
            try
            {
                var updated = await _designService.UpdateDesignAsync(id, updateDesign);
                return updated ? NoContent() : NotFound("Diseño no encontrado.");
            }
            catch (ContractValidationException ex)
            {
                return BadRequest(new
                {
                    Error = "Contrato de diseño inválido.",
                    Details = ex.Errors
                });
            }
        }

        [SwaggerOperation(Summary = "Eliminar una plantilla", Description = "Elimina permanentemente una plantilla del sistema.")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _designService.DeleteDesignAsync(id);
            return deleted ? NoContent() : NotFound("Diseño no encontrado.");
        }
    }
}

