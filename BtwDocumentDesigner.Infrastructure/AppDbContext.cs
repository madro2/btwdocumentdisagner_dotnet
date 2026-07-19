namespace BtwDocumentDesigner.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<PdfDesignTemplate> PdfDesignTemplates => Set<PdfDesignTemplate>();
        public DbSet<PdfDesignImage> PdfDesignImages => Set<PdfDesignImage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<PdfDesignTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Composite unique index for name and version
                entity.HasIndex(e => new { e.DesignName, e.DesignVersion }).IsUnique();
            });

            modelBuilder.Entity<PdfDesignImage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ResourceKey).IsUnique();
            });
        }
    }
}
