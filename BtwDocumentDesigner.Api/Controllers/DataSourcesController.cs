namespace BtwDocumentDesigner.Api.Controllers
{
    [ApiController]
    [Route("DocumentDesignerApi/data-sources")]
    public sealed class DataSourcesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DataSourcesController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<DataSourceCollectionDto>>> GetAll()
        {
            var collections = await _db.DataSourceCollections
                .AsNoTracking()
                .Include(collection => collection.Fields)
                .OrderBy(collection => collection.Name)
                .ToListAsync();

            return Ok(collections.Select(ToDto));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DataSourceCollectionDto>> GetById(Guid id)
        {
            var collection = await _db.DataSourceCollections
                .AsNoTracking()
                .Include(item => item.Fields)
                .SingleOrDefaultAsync(item => item.Id == id);

            return collection is null ? NotFound() : Ok(ToDto(collection));
        }

        [HttpPost]
        public async Task<ActionResult<DataSourceCollectionDto>> Create(
            [FromBody] SaveDataSourceCollectionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("El nombre de la colección es obligatorio.");
            }

            if (await _db.DataSourceCollections.AnyAsync(
                item => item.Name == request.Name.Trim()))
            {
                return Conflict("Ya existe una colección con ese nombre.");
            }

            var collection = new DataSourceCollection
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                SourceType = request.SourceType?.Trim() ?? "XML/JSON"
            };
            ApplyFields(collection, request.Fields);
            _db.DataSourceCollections.Add(collection);
            await _db.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = collection.Id },
                ToDto(collection));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<DataSourceCollectionDto>> Update(
            Guid id,
            [FromBody] SaveDataSourceCollectionRequest request)
        {
            var collection = await _db.DataSourceCollections
                .Include(item => item.Fields)
                .SingleOrDefaultAsync(item => item.Id == id);
            if (collection is null)
            {
                return NotFound();
            }

            collection.Name = request.Name.Trim();
            collection.Description = request.Description?.Trim() ?? string.Empty;
            collection.SourceType = request.SourceType?.Trim() ?? "XML/JSON";
            collection.ModificationDate = DateTime.UtcNow;
            _db.DataSourceFields.RemoveRange(collection.Fields);
            collection.Fields.Clear();
            ApplyFields(collection, request.Fields);
            await _db.SaveChangesAsync();
            return Ok(ToDto(collection));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var collection = await _db.DataSourceCollections.FindAsync(id);
            if (collection is null)
            {
                return NotFound();
            }

            _db.DataSourceCollections.Remove(collection);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static void ApplyFields(
            DataSourceCollection collection,
            IEnumerable<SaveDataSourceFieldRequest>? requests)
        {
            var fields = requests?.ToList() ?? [];
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                if (string.IsNullOrWhiteSpace(field.Path))
                {
                    continue;
                }

                collection.Fields.Add(new DataSourceField
                {
                    Name = field.Name?.Trim() ?? string.Empty,
                    DisplayName = field.DisplayName?.Trim() ?? field.Name?.Trim() ?? string.Empty,
                    Description = field.Description?.Trim() ?? string.Empty,
                    Path = field.Path.Trim(),
                    DataType = field.DataType?.Trim() ?? "String",
                    Cardinality = field.Cardinality?.Trim() ?? string.Empty,
                    Group = field.Group?.Trim() ?? string.Empty,
                    SortOrder = field.SortOrder > 0 ? field.SortOrder : index + 1
                });
            }
        }

        private static DataSourceCollectionDto ToDto(DataSourceCollection collection)
        {
            return new DataSourceCollectionDto(
                collection.Id,
                collection.Name,
                collection.Description,
                collection.SourceType,
                collection.Fields
                    .OrderBy(field => field.SortOrder)
                    .Select(field => new DataSourceFieldDto(
                        field.Id,
                        field.Name,
                        field.DisplayName,
                        field.Description,
                        field.Path,
                        field.DataType,
                        field.Cardinality,
                        field.Group,
                        field.SortOrder))
                    .ToList());
        }
    }

    public sealed record DataSourceCollectionDto(
        Guid Id,
        string Name,
        string Description,
        string SourceType,
        IReadOnlyList<DataSourceFieldDto> Fields);

    public sealed record DataSourceFieldDto(
        Guid Id,
        string Name,
        string DisplayName,
        string Description,
        string Path,
        string DataType,
        string Cardinality,
        string Group,
        int SortOrder);

    public sealed record SaveDataSourceCollectionRequest(
        string Name,
        string? Description,
        string? SourceType,
        IReadOnlyList<SaveDataSourceFieldRequest>? Fields);

    public sealed record SaveDataSourceFieldRequest(
        string? Name,
        string? DisplayName,
        string? Description,
        string Path,
        string? DataType,
        string? Cardinality,
        string? Group,
        int SortOrder);
}
