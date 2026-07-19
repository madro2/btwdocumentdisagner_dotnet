namespace BtwDocumentDesigner.Domain
{
    public class PdfDesignTemplate
    {
        [Display(Name = "Identificador", Description = "Código único de la plantilla.")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Display(Name = "Tipo de documento", Description = "Clase de documento que se generará, por ejemplo factura o recibo.")]
        public string DocumentType { get; set; } = string.Empty;

        [Display(Name = "Nombre de la plantilla", Description = "Nombre con el que las personas reconocerán este diseño.")]
        public string DesignName { get; set; } = string.Empty;

        [Display(Name = "Versión", Description = "Número de versión de la plantilla.")]
        public int DesignVersion { get; set; }

        [Display(Name = "Configuración del diseño", Description = "Estructura JSON que define el contenido y la apariencia del PDF.")]
        public string JsonConfiguration { get; set; } = string.Empty;

        [Display(Name = "Fecha de creación", Description = "Fecha y hora en que se creó la plantilla.")]
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Fecha de última modificación", Description = "Fecha y hora del último cambio realizado.")]
        public DateTime? ModificationDate { get; set; }

        [Display(Name = "Modificado por", Description = "Persona o sistema que realizó el último cambio.")]
        public string? ModificationUser { get; set; }
    }
}
