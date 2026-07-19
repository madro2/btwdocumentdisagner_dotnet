namespace BtwDocumentDesigner.Domain.Options
{
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        [Display(Name = "Cadena de conexión", Description = "Datos técnicos de conexión a la base de datos.")]
        public string ConnectionString { get; set; } = string.Empty;
    }
}
