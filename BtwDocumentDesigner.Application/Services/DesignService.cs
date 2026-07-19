namespace BtwDocumentDesigner.Application.Services
{
    public class DesignService : IDesignService
    {
        private readonly IDesignRepository _designRepository;
        private readonly ContractValidationService _contractValidator;

        public DesignService(
            IDesignRepository designRepository,
            ContractValidationService? contractValidator = null)
        {
            _designRepository = designRepository;
            _contractValidator = contractValidator ?? new ContractValidationService();
        }

        public async Task<PdfDesignTemplate> CreateDesignAsync(PdfDesignTemplate design)
        {
            ValidateConfiguration(design.JsonConfiguration);
            var exists = await _designRepository.ExistsAsync(design.DesignName, design.DesignVersion);
            if (exists)
                throw new InvalidOperationException(
                    "Ya existe un diseño con ese nombre y versión.");

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
            ValidateConfiguration(updateDesign.JsonConfiguration);
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

        private void ValidateConfiguration(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var schema = System.Text.Json.JsonSerializer.Deserialize<PdfDesignSchema>(
                json,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            if (schema is null)
                throw new ContractValidationException(["Diseño JSON inválido."]);
            _contractValidator.ValidateAndThrow(schema);
        }

        public async Task<bool> DeleteDesignAsync(Guid id)
        {
            var design = await _designRepository.GetByIdAsync(id);
            if (design == null) return false;

            await _designRepository.DeleteAsync(design);
            return true;
        }
    }
}
