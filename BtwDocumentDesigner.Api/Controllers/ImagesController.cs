namespace BtwDocumentDesigner.Api.Controllers
{
    [ApiController]
    [Route("DocumentDesignerApi/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly IImageService _imageService;

        public ImagesController(IImageService imageService)
        {
            _imageService = imageService;
        }

        [SwaggerOperation(Summary = "Cargar una imagen", Description = "Guarda una imagen, como un logo o una firma, para usarla en las plantillas PDF.")]
        [HttpPost]
        public async Task<IActionResult> Upload(
            [Display(Name = "Archivo de imagen")] IFormFile file,
            [FromQuery, Display(Name = "Clave del recurso")] string? resourceKey = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest("El archivo de imagen no es válido.");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            var savedImage = await _imageService.SaveImageAsync(
                file.FileName,
                file.ContentType,
                memoryStream.ToArray(),
                resourceKey);

            return Ok(new { Id = savedImage.Id, savedImage.ResourceKey });
        }

        [SwaggerOperation(Summary = "Consultar una imagen", Description = "Descarga una imagen usando su identificador.")]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var image = await _imageService.GetImageAsync(id);
            if (image == null) return NotFound("Imagen no encontrada.");

            return File(image.ImageData, image.ContentType, image.FileName);
        }

        [SwaggerOperation(Summary = "Eliminar una imagen", Description = "Elimina permanentemente una imagen guardada.")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _imageService.DeleteImageAsync(id);
            return deleted ? NoContent() : NotFound("Imagen no encontrada.");
        }
    }
}

