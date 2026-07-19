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
            if (image != null && image.ImageData.Length == 0)
            {
                // Populate ImageData from file for legacy/controller usage
                var filePath = Path.Combine(_uploadFolder, $"{image.Id}{Path.GetExtension(image.FileName)}");
                if (File.Exists(filePath))
                {
                    image.ImageData = await File.ReadAllBytesAsync(filePath);
                }
            }
            return image;
        }

        public async Task<byte[]?> GetImageDataAsync(Guid id)
        {
            var image = await GetByIdAsync(id);
            return image?.ImageData;
        }

        public async Task<byte[]?> GetImageDataByKeyAsync(string key)
        {
            var image = await _db.PdfDesignImages
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.ResourceKey == key);
            if (image is null) return null;

            // Las imágenes nuevas se almacenan en wwwroot/uploads para evitar
            // duplicar binarios en PostgreSQL. Reutilizamos la misma lectura
            // que el endpoint de descarga para que el motor PDF pueda resolver
            // recursos por su clave estable.
            return (await GetByIdAsync(image.Id))?.ImageData;
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
