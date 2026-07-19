using System.Text.Json;
using System.Text.Json.Nodes;
using BtwDocumentDesigner.Domain;
using BtwDocumentDesigner.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BtwDocumentDesigner.Api.Data;

public static class DatabaseSeeder
{
    public const string NationalInvoiceName = "Factura electrónica nacional";
    public const int NationalInvoiceVersion = 1;

    private static readonly Guid TemplateId =
        Guid.Parse("f1000000-0000-0000-0000-000000000001");
    private static readonly Guid HeaderLogoId =
        Guid.Parse("f2000000-0000-0000-0000-000000000001");
    private static readonly Guid FooterLogoId =
        Guid.Parse("f2000000-0000-0000-0000-000000000002");
    private static readonly Guid BasicFieldsCollectionId =
        Guid.Parse("d1000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(
        AppDbContext db,
        IWebHostEnvironment environment)
    {
        var seedDirectory = Path.Combine(environment.ContentRootPath, "SeedData");
        var contractPath = Path.Combine(
            seedDirectory,
            "factura-electronica-nacional.json");
        var logoPath = Path.Combine(seedDirectory, "btw-logo.png");
        var basicFieldsPath = Path.Combine(
            seedDirectory,
            "data-source-basic-fields.json");

        if (
            !File.Exists(contractPath)
            || !File.Exists(logoPath)
            || !File.Exists(basicFieldsPath)
        )
        {
            throw new InvalidOperationException(
                "No se encontraron los archivos semilla de la factura electrónica nacional.");
        }

        await EnsureImageAsync(
            db,
            environment,
            HeaderLogoId,
            "factura-nacional-encabezado.png",
            logoPath);
        await EnsureImageAsync(
            db,
            environment,
            FooterLogoId,
            "factura-nacional-pie.png",
            logoPath);

        var exists = await db.PdfDesignTemplates.AnyAsync(
            template =>
                template.DesignName == NationalInvoiceName
                && template.DesignVersion == NationalInvoiceVersion);
        if (!exists)
        {
            var contract = JsonNode.Parse(await File.ReadAllTextAsync(contractPath))
                ?? throw new InvalidOperationException(
                    "El contrato JSON de la factura nacional no es válido.");
            var document = contract["document"]?.AsObject()
                ?? throw new InvalidOperationException(
                    "El contrato no contiene la sección document.");
            document["id"] = TemplateId.ToString();
            document["name"] = NationalInvoiceName;
            document["type"] = "invoice";
            document["version"] = NationalInvoiceVersion;

            ReplaceAssetIds(contract["components"], HeaderLogoId, FooterLogoId);
            if (contract["pages"] is JsonArray pages)
            {
                foreach (var pageNode in pages)
                {
                    if (pageNode is JsonObject pageObject)
                    {
                        ReplaceAssetIds(
                            pageObject["components"],
                            HeaderLogoId,
                            FooterLogoId);
                    }
                }
            }

            db.PdfDesignTemplates.Add(
                new PdfDesignTemplate
                {
                    Id = TemplateId,
                    DocumentType = "invoice",
                    DesignName = NationalInvoiceName,
                    DesignVersion = NationalInvoiceVersion,
                    JsonConfiguration = contract.ToJsonString(
                        new JsonSerializerOptions { WriteIndented = false }),
                    CreationDate = DateTime.UtcNow,
                    ModificationUser = "system-seed"
                });
            await db.SaveChangesAsync();
        }

        await SeedBasicDataSourceAsync(db, basicFieldsPath);
    }

    internal static async Task SeedBasicDataSourceAsync(
        AppDbContext db,
        string seedPath)
    {
        var seed = JsonSerializer.Deserialize<BasicDataSourceSeed>(
            await File.ReadAllTextAsync(seedPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(
                "El catálogo semilla de fuentes de datos no es válido.");
        var duplicatePath = seed.Fields
            .GroupBy(field => field.Path.Trim(), StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePath is not null)
        {
            throw new InvalidOperationException(
                $"El catálogo semilla contiene el path duplicado '{duplicatePath.Key}'.");
        }

        var collection = await db.DataSourceCollections
            .Include(item => item.Fields)
            .SingleOrDefaultAsync(item => item.Id == BasicFieldsCollectionId);
        if (collection is null)
        {
            collection = new DataSourceCollection
            {
                Id = BasicFieldsCollectionId,
                CreationDate = DateTime.UtcNow
            };
            db.DataSourceCollections.Add(collection);
        }
        else
        {
            collection.ModificationDate = DateTime.UtcNow;
        }

        collection.Name = seed.Name.Trim();
        collection.Description = seed.Description.Trim();
        collection.SourceType = seed.SourceType.Trim();

        var existingByPath = collection.Fields.ToDictionary(
            field => field.Path,
            StringComparer.Ordinal);
        var seededPaths = seed.Fields
            .Select(field => field.Path.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var obsoleteFields = collection.Fields
            .Where(field => !seededPaths.Contains(field.Path))
            .ToList();
        if (obsoleteFields.Count > 0)
        {
            db.DataSourceFields.RemoveRange(obsoleteFields);
        }

        foreach (var field in seed.Fields.OrderBy(item => item.SortOrder))
        {
            var path = field.Path.Trim();
            if (!existingByPath.TryGetValue(path, out var entity))
            {
                entity = new DataSourceField { Path = path };
                collection.Fields.Add(entity);
                db.DataSourceFields.Add(entity);
            }

            entity.Name = field.Name.Trim();
            entity.DisplayName = field.DisplayName.Trim();
            entity.Description = field.Description.Trim();
            entity.DataType = field.DataType.Trim();
            entity.Cardinality = field.Cardinality.Trim();
            entity.Group = field.Group.Trim();
            entity.SortOrder = field.SortOrder;
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureImageAsync(
        AppDbContext db,
        IWebHostEnvironment environment,
        Guid id,
        string fileName,
        string sourcePath)
    {
        var uploadDirectory = Path.Combine(
            environment.ContentRootPath,
            "wwwroot",
            "uploads");
        Directory.CreateDirectory(uploadDirectory);
        var destination = Path.Combine(
            uploadDirectory,
            $"{id}{Path.GetExtension(fileName)}");
        if (!File.Exists(destination))
        {
            File.Copy(sourcePath, destination);
        }

        if (await db.PdfDesignImages.AnyAsync(image => image.Id == id))
        {
            return;
        }

        db.PdfDesignImages.Add(
            new PdfDesignImage
            {
                Id = id,
                FileName = fileName,
                ContentType = "image/png",
                ImageData = Array.Empty<byte>(),
                UploadDate = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
    }

    private static void ReplaceAssetIds(
        JsonNode? node,
        Guid headerLogoId,
        Guid footerLogoId)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                ReplaceAssetIds(child, headerLogoId, footerLogoId);
            }
            return;
        }

        if (node is not JsonObject component)
        {
            return;
        }

        if (component["content"] is JsonObject content)
        {
            var assetId = content["assetId"]?.GetValue<string>();
            if (assetId == "logo-empresa")
            {
                content["assetId"] = headerLogoId.ToString();
            }
            else if (assetId == "logo-pie")
            {
                content["assetId"] = footerLogoId.ToString();
            }
        }

        ReplaceAssetIds(component["components"], headerLogoId, footerLogoId);
    }

    private sealed record BasicDataSourceSeed(
        string Name,
        string Description,
        string SourceType,
        IReadOnlyList<BasicDataSourceFieldSeed> Fields);

    private sealed record BasicDataSourceFieldSeed(
        string Name,
        string DisplayName,
        string Description,
        string Path,
        string DataType,
        string Cardinality,
        string Group,
        int SortOrder);
}
