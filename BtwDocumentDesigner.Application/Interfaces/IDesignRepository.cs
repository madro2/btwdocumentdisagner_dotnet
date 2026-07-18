namespace BtwDocumentDesigner.Application.Interfaces
{
    public interface IDesignRepository
    {
        Task AddAsync(PdfDesignTemplate design);
        Task<bool> ExistsAsync(string name, int version);
        Task<IEnumerable<PdfDesignTemplate>> GetAllAsync();
        Task<PdfDesignTemplate?> GetByIdAsync(Guid id);
        Task<PdfDesignTemplate?> SearchAsync(string name, int version);
        Task UpdateAsync(PdfDesignTemplate design);
        Task DeleteAsync(PdfDesignTemplate design);
    }
}
