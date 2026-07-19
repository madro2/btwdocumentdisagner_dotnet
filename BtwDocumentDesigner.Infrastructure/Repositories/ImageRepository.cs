namespace BtwDocumentDesigner.Infrastructure.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private readonly AppDbContext _db;

        public ImageRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(PdfDesignImage image)
        {
            _db.PdfDesignImages.Add(image);
            await _db.SaveChangesAsync();
        }

        public async Task<PdfDesignImage?> GetByIdAsync(Guid id)
        {
            return await _db.PdfDesignImages.FindAsync(id);
        }

        public async Task<byte[]?> GetImageDataAsync(Guid id)
        {
            var image = await _db.PdfDesignImages.FindAsync(id);
            return image?.ImageData;
        }

        public async Task<byte[]?> GetImageDataByKeyAsync(string key)
        {
            var image = await _db.PdfDesignImages
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.ResourceKey == key);
            return image?.ImageData;
        }

        public async Task DeleteAsync(PdfDesignImage image)
        {
            _db.PdfDesignImages.Remove(image);
            await _db.SaveChangesAsync();
        }
    }
}
