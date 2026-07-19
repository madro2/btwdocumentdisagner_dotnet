using BtwDocumentDesigner.Application.Interfaces;
using BtwDocumentDesigner.Domain;
using Microsoft.EntityFrameworkCore;

namespace BtwDocumentDesigner.Infrastructure.Repositories
{
    public class SystemDefaultValueRepository : ISystemDefaultValueRepository
    {
        private readonly AppDbContext _db;

        public SystemDefaultValueRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Dictionary<string, string>> GetAllAsync()
        {
            return await _db.SystemDefaultValues
                .ToDictionaryAsync(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }

        public async Task AddOrUpdateAsync(string key, string value)
        {
            var entity = await _db.SystemDefaultValues
                .SingleOrDefaultAsync(item => item.Key == key);
            
            if (entity == null)
            {
                entity = new SystemDefaultValue { Key = key, Value = value };
                _db.SystemDefaultValues.Add(entity);
            }
            else
            {
                entity.Value = value;
            }
            
            await _db.SaveChangesAsync();
        }
    }
}
