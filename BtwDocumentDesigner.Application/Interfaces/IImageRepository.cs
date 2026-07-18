namespace BtwDocumentDesigner.Application.Interfaces
{
    public interface IImageRepository
    {
        Task AddAsync(PdfDesignImage image);
        Task<PdfDesignImage?> GetByIdAsync(Guid id);
        Task<byte[]?> GetImageDataAsync(Guid id);
        Task DeleteAsync(PdfDesignImage image);
    }
}
