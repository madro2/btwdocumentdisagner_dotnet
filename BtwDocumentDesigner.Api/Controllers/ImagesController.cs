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

        [SwaggerOperation(Summary = "Sube una imagen", Description = "Permite cargar una nueva imagen (logo, firma, etc) para usar en los PDFs.")]
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("El archivo de imagen no es válido.");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            var savedImage = await _imageService.SaveImageAsync(file.FileName, file.ContentType, memoryStream.ToArray());

            return Ok(new { Id = savedImage.Id });
        }

        [SwaggerOperation(Summary = "Obtiene una imagen", Description = "Devuelve el archivo binario de la imagen por su identificador.")]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var image = await _imageService.GetImageAsync(id);
            if (image == null) return NotFound("Imagen no encontrada.");

            return File(image.ImageData, image.ContentType, image.FileName);
        }

        [SwaggerOperation(Summary = "Elimina una imagen", Description = "Borra una imagen permanentemente de la base de datos.")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _imageService.DeleteImageAsync(id);
            return deleted ? NoContent() : NotFound("Imagen no encontrada.");
        }
    }
}

