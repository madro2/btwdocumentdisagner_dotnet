namespace BtwDocumentDesigner.Application.Interfaces
{
    public interface IImageService
    {
        Task<PdfDesignImage> SaveImageAsync(string fileName, string contentType, byte[] data);
        Task<PdfDesignImage?> GetImageAsync(Guid id);
        Task<bool> DeleteImageAsync(Guid id);
    }
}
