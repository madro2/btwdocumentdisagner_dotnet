namespace BtwDocumentDesigner.Application.Interfaces
{
    public interface ISystemDefaultValueRepository
    {
        Task<Dictionary<string, string>> GetAllAsync();
        Task AddOrUpdateAsync(string key, string value);
    }
}
