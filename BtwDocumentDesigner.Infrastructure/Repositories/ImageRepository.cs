using System.IO;

namespace BtwDocumentDesigner.Infrastructure.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private readonly AppDbContext _db;
        private readonly string _uploadFolder;

        public ImageRepository(AppDbContext db)
        {
            _db = db;
            _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(_uploadFolder))
            {
                Directory.CreateDirectory(_uploadFolder);
            }
        }

        public async Task AddAsync(PdfDesignImage image)
        {
            var filePath = Path.Combine(_uploadFolder, $"{image.Id}{Path.GetExtension(image.FileName)}");
            await File.WriteAllBytesAsync(filePath, image.ImageData);
            
            image.ImageData = Array.Empty<byte>(); // Clear before saving to DB to save space
            _db.PdfDesignImages.Add(image);
            await _db.SaveChangesAsync();
        }

        public async Task<PdfDesignImage?> GetByIdAsync(Guid id)
        {
            var image = await _db.PdfDesignImages.FindAsync(id);
            if (image != null && (image.ImageData == null || image.ImageData.Length == 0))
            {
                // Populate ImageData from file for legacy/controller usage
                var filePath = Path.Combine(_uploadFolder, $"{image.Id}{Path.GetExtension(image.FileName)}");
                if (File.Exists(filePath))
                {
                    image.ImageData = await File.ReadAllBytesAsync(filePath);
                }
                else
                {
                    image.ImageData = Array.Empty<byte>();
                }
            }
            return image;
        }

        public async Task<byte[]?> GetImageDataAsync(Guid id)
        {
            var image = await GetByIdAsync(id);
            return image?.ImageData;
        }

        public async Task DeleteAsync(PdfDesignImage image)
        {
            var filePath = Path.Combine(_uploadFolder, $"{image.Id}{Path.GetExtension(image.FileName)}");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            
            _db.PdfDesignImages.Remove(image);
            await _db.SaveChangesAsync();
        }
    }
}
