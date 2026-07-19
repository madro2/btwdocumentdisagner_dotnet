namespace BtwDocumentDesigner.Application.Services
{
    public class DesignService : IDesignService
    {
        private readonly IDesignRepository _designRepository;

        public DesignService(IDesignRepository designRepository)
        {
            _designRepository = designRepository;
        }

        public async Task<PdfDesignTemplate> CreateDesignAsync(PdfDesignTemplate design)
        {
            var exists = await _designRepository.ExistsAsync(design.DesignName, design.DesignVersion);
            if (exists) throw new InvalidOperationException("Ya existe un diseño con ese nombre y versión.");

            design.CreationDate = DateTime.UtcNow;
            design.ModificationDate = null;
            
            await _designRepository.AddAsync(design);
            return design;
        }

        public async Task<IEnumerable<PdfDesignTemplate>> GetAllDesignsAsync()
        {
            return await _designRepository.GetAllAsync();
        }

        public async Task<PdfDesignTemplate?> GetDesignByIdAsync(Guid id)
        {
            return await _designRepository.GetByIdAsync(id);
        }

        public async Task<PdfDesignTemplate?> SearchDesignAsync(string name, int version)
        {
            return await _designRepository.SearchAsync(name, version);
        }

        public async Task<bool> UpdateDesignAsync(Guid id, PdfDesignTemplate updateDesign)
        {
            var design = await _designRepository.GetByIdAsync(id);
            if (design == null) return false;

            design.DocumentType = updateDesign.DocumentType;
            design.DesignName = updateDesign.DesignName;
            design.DesignVersion = updateDesign.DesignVersion;
            design.JsonConfiguration = updateDesign.JsonConfiguration;
            design.ModificationDate = DateTime.UtcNow;
            design.ModificationUser = updateDesign.ModificationUser;

            await _designRepository.UpdateAsync(design);
            return true;
        }

        public async Task<bool> DeleteDesignAsync(Guid id)
        {
            var design = await _designRepository.GetByIdAsync(id);
            if (design == null) return false;

            if (design.DesignVersion == 1)
            {
                throw new InvalidOperationException("No se permite eliminar la versión 1 (base) de una plantilla.");
            }

            await _designRepository.DeleteAsync(design);
            return true;
        }
    }
}
