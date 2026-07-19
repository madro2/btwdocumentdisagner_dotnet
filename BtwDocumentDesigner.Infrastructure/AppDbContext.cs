namespace BtwDocumentDesigner.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<PdfDesignTemplate> PdfDesignTemplates => Set<PdfDesignTemplate>();
        public DbSet<PdfDesignImage> PdfDesignImages => Set<PdfDesignImage>();
        public DbSet<DataSourceCollection> DataSourceCollections => Set<DataSourceCollection>();
        public DbSet<DataSourceField> DataSourceFields => Set<DataSourceField>();

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

            modelBuilder.Entity<DataSourceCollection>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(180);
                entity.Property(e => e.SourceType).HasMaxLength(40);
            });

            modelBuilder.Entity<DataSourceField>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.DataSourceCollectionId, e.Path }).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(180);
                entity.Property(e => e.DisplayName).HasMaxLength(240);
                entity.Property(e => e.Path).HasMaxLength(500);
                entity.Property(e => e.DataType).HasMaxLength(80);
                entity.Property(e => e.Cardinality).HasMaxLength(20);
                entity.Property(e => e.Group).HasMaxLength(180);
                entity.HasOne(e => e.Collection)
                    .WithMany(e => e.Fields)
                    .HasForeignKey(e => e.DataSourceCollectionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
