namespace BtwDocumentDesigner.Application.Services
{
    public class ImageService : IImageService
    {
        private readonly IImageRepository _imageRepository;

        public ImageService(IImageRepository imageRepository)
        {
            _imageRepository = imageRepository;
        }

        public async Task<PdfDesignImage> SaveImageAsync(string fileName, string contentType, byte[] data)
        {
            var pdfImage = new PdfDesignImage
            {
                FileName = fileName,
                ContentType = contentType,
                ImageData = data
            };

            await _imageRepository.AddAsync(pdfImage);
            return pdfImage;
        }

        public async Task<PdfDesignImage?> GetImageAsync(Guid id)
        {
            return await _imageRepository.GetByIdAsync(id);
        }

        public async Task<bool> DeleteImageAsync(Guid id)
        {
            var image = await _imageRepository.GetByIdAsync(id);
            if (image == null) return false;

            await _imageRepository.DeleteAsync(image);
            return true;
        }
    }
}
