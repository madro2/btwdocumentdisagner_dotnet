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

        [SwaggerOperation(Summary = "Crea un nuevo diseño", Description = "Registra un nuevo template de diseño de PDF en el sistema.")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PdfDesignTemplate design)
        {
            try
            {
                var created = await _designService.CreateDesignAsync(design);
                return Created($"/api/designs/{created.Id}", created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [SwaggerOperation(Summary = "Obtiene todos los diseños", Description = "Devuelve una lista con todos los diseños registrados.")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _designService.GetAllDesignsAsync());
        }

        [SwaggerOperation(Summary = "Obtiene un diseño por ID", Description = "Busca un diseño específico utilizando su identificador único.")]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var design = await _designService.GetDesignByIdAsync(id);
            return design != null ? Ok(design) : NotFound("Diseño no encontrado.");
        }

        [SwaggerOperation(Summary = "Busca un diseño por nombre y versión", Description = "Permite consultar un diseño filtrando por su nombre exacto y versión.")]
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string name, [FromQuery] int version)
        {
            var design = await _designService.SearchDesignAsync(name, version);
            return design != null ? Ok(new { Id = design.Id }) : NotFound("Diseño no encontrado.");
        }

        [SwaggerOperation(Summary = "Actualiza un diseño existente", Description = "Sobrescribe la configuración de un diseño por su ID.")]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PdfDesignTemplate updateDesign)
        {
            var updated = await _designService.UpdateDesignAsync(id, updateDesign);
            return updated ? NoContent() : NotFound("Diseño no encontrado.");
        }

        [SwaggerOperation(Summary = "Elimina un diseño", Description = "Borra un diseño del sistema permanentemente por su ID.")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var deleted = await _designService.DeleteDesignAsync(id);
                return deleted ? NoContent() : NotFound("Diseño no encontrado.");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error eliminando el diseño: {ex.Message}");
            }
        }
    }
}

