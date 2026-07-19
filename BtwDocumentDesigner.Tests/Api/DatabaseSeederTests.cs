using BtwDocumentDesigner.Api.Data;
using BtwDocumentDesigner.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BtwDocumentDesigner.Tests.Api;

public sealed class DatabaseSeederTests
{
    [Fact]
    public async Task BasicDataSourceSeed_UpdatesExistingFieldsWithoutDuplicates()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var seedPath = Path.Combine(
            Path.GetTempPath(),
            $"data-source-seed-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(
                seedPath,
                SeedJson(
                    Field("Company", "Compañía", "Descripción inicial", "InvcHead.Company", 1),
                    Field("InvoiceNum", "Factura", "Número interno", "InvcHead.InvoiceNum", 2)));
            await DatabaseSeeder.SeedBasicDataSourceAsync(db, seedPath);

            await File.WriteAllTextAsync(
                seedPath,
                SeedJson(
                    Field("Company", "Empresa", "Descripción actualizada", "InvcHead.Company", 1),
                    Field("LegalNumber", "Número legal", "Número legal", "InvcHead.LegalNumber", 2)));
            await DatabaseSeeder.SeedBasicDataSourceAsync(db, seedPath);

            var collection = await db.DataSourceCollections
                .Include(item => item.Fields)
                .SingleAsync();
            Assert.Equal("Campos básicos", collection.Name);
            Assert.Equal(2, collection.Fields.Count);
            Assert.Equal(
                "Descripción actualizada",
                collection.Fields.Single(field => field.Path == "InvcHead.Company").Description);
            Assert.DoesNotContain(
                collection.Fields,
                field => field.Path == "InvcHead.InvoiceNum");
            Assert.Contains(
                collection.Fields,
                field => field.Path == "InvcHead.LegalNumber");
        }
        finally
        {
            File.Delete(seedPath);
        }
    }

    private static string SeedJson(params string[] fields)
    {
        return $$"""
        {
          "name": "Campos básicos",
          "description": "Catálogo de prueba",
          "sourceType": "XML/JSON",
          "fields": [{{string.Join(",", fields)}}]
        }
        """;
    }

    private static string Field(
        string name,
        string displayName,
        string description,
        string path,
        int sortOrder)
    {
        return $$"""
        {
          "name": "{{name}}",
          "displayName": "{{displayName}}",
          "description": "{{description}}",
          "path": "{{path}}",
          "dataType": "String",
          "cardinality": "1..1",
          "group": "InvcHead",
          "sortOrder": {{sortOrder}}
        }
        """;
    }
}
