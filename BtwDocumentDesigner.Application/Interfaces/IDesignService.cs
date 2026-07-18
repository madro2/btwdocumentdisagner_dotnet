namespace BtwDocumentDesigner.Application.Interfaces
{
    public interface IDesignService
    {
        Task<PdfDesignTemplate> CreateDesignAsync(PdfDesignTemplate design);
        Task<IEnumerable<PdfDesignTemplate>> GetAllDesignsAsync();
        Task<PdfDesignTemplate?> GetDesignByIdAsync(Guid id);
        Task<PdfDesignTemplate?> SearchDesignAsync(string name, int version);
        Task<bool> UpdateDesignAsync(Guid id, PdfDesignTemplate updateDesign);
        Task<bool> DeleteDesignAsync(Guid id);
    }
}
