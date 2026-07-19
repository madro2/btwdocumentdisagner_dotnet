namespace BtwDocumentDesigner.Domain
{
    public class PdfDesignImage
    {
        [Display(Name = "Identificador", Description = "Código único de la imagen.")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Display(Name = "Nombre del archivo", Description = "Nombre original del archivo de imagen.")]
        public string FileName { get; set; } = string.Empty;

        [Display(Name = "Clave del recurso", Description = "Nombre estable usado por la plantilla, por ejemplo logo-empresa.")]
        public string? ResourceKey { get; set; }

        [Display(Name = "Tipo de archivo", Description = "Formato de la imagen, por ejemplo image/png o image/jpeg.")]
        public string ContentType { get; set; } = string.Empty;

        [Display(Name = "Contenido de la imagen", Description = "Datos binarios del archivo cargado.")]
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        [Display(Name = "Fecha de carga", Description = "Fecha y hora en que se cargó la imagen.")]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    }
}
