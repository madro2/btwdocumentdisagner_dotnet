namespace BtwDocumentDesigner.Infrastructure.Repositories
{
    public class DesignRepository : IDesignRepository
    {
        private readonly AppDbContext _db;

        public DesignRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(PdfDesignTemplate design)
        {
            _db.PdfDesignTemplates.Add(design);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(string name, int version)
        {
            return await _db.PdfDesignTemplates.AnyAsync(d => d.DesignName == name && d.DesignVersion == version);
        }

        public async Task<IEnumerable<PdfDesignTemplate>> GetAllAsync()
        {
            return await _db.PdfDesignTemplates.ToListAsync();
        }

        public async Task<PdfDesignTemplate?> GetByIdAsync(Guid id)
        {
            return await _db.PdfDesignTemplates.FindAsync(id);
        }

        public async Task<PdfDesignTemplate?> SearchAsync(string name, int version)
        {
            return await _db.PdfDesignTemplates.FirstOrDefaultAsync(d => d.DesignName == name && d.DesignVersion == version);
        }

        public async Task UpdateAsync(PdfDesignTemplate design)
        {
            _db.PdfDesignTemplates.Update(design);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(PdfDesignTemplate design)
        {
            _db.PdfDesignTemplates.Remove(design);
            await _db.SaveChangesAsync();
        }
    }
}
