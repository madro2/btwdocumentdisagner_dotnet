using System.Text.Json;
using System.Xml.Linq;
using BtwDocumentDesigner.Application.Interfaces;
using BtwDocumentDesigner.Application.Rendering;
using Moq;
using PdfSharp.Pdf.IO;

namespace BtwDocumentDesigner.Tests.Application;

public sealed class BindingAndRenderingTests
{
    [Fact]
    public void Resolver_UsesDirectXmlRowsComputedFieldsAndFilters()
    {
        var schema = CreateSchema();
        var xml = XDocument.Parse(
            """
            <NewDataSet>
              <Company><StateTaxID>900665411</StateTaxID><Name>Bythewave Sas</Name></Company>
              <InvcDtl><LineDesc>Servicio dinámico</LineDesc><ExtPrice>100</ExtPrice><TaxAmtLineIVA>19</TaxAmtLineIVA></InvcDtl>
            </NewDataSet>
            """);
        var resolver = new BindingResolver(schema, xml, null);

        Assert.Equal("Bythewave Sas", resolver.Render("{{Company.Name}}"));
        Assert.Equal("2", resolver.Render("{{Computed.IssuerCheckDigit}}"));
        var row = Assert.Single(resolver.Collection("InvcDtl"));
        var scope = new BindingScope(row, "Row");
        Assert.Equal(
            "19 %",
            resolver.Render("{{Row.TaxAmtLineIVA|percentageOf:Row.ExtPrice|number:0}} %", scope));
    }

    [Fact]
    public async Task Engine_UsesPhysicalPageSizeFromContract()
    {
        var schema = CreateSchema();
        schema.Page = new PageSettings
        {
            Size = "LETTER",
            WidthMm = 215.9,
            HeightMm = 279.4,
            MarginsMm = new Margins { Top = 7, Right = 7, Bottom = 7, Left = 7 }
        };
        schema.Components =
        [
            new PdfComponent
            {
                Id = "name",
                Type = "text",
                Position = new Position { X = 10, Y = 10, Width = 80, Height = 10 },
                Content = new ComponentContent { Value = "{{Company.Name}}" },
                Style = new ComponentStyle { FontFamily = "Arial", FontSizePt = 10 }
            }
        ];
        var json = JsonSerializer.Serialize(schema);
        var images = new Mock<IImageRepository>();
        var engine = new PdfRenderingEngine(images.Object, new ContractValidationService());

        var bytes = await engine.GeneratePdfAsync(
            json,
            "<NewDataSet><Company><Name>Bythewave Sas</Name></Company></NewDataSet>",
            "application/xml");

        using var stream = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.Single(pdf.Pages);
        Assert.InRange(pdf.Pages[0].Width.Millimeter, 215.8, 216.0);
        Assert.InRange(pdf.Pages[0].Height.Millimeter, 279.3, 279.5);
    }

    [Fact]
    public void Validator_RejectsDuplicateIdsAndInvalidTableWidths()
    {
        var schema = CreateSchema();
        schema.Components =
        [
            new PdfComponent
            {
                Id = "duplicate",
                Type = "text",
                Position = new Position { Width = 10, Height = 10 }
            },
            new PdfComponent
            {
                Id = "duplicate",
                Type = "table",
                Position = new Position { Width = 40, Height = 10 },
                Columns =
                [
                    new TableColumn { Id = "a", WidthMm = 10 },
                    new TableColumn { Id = "b", WidthMm = 10 }
                ]
            }
        ];

        var errors = new ContractValidationService().Validate(schema);

        Assert.Contains(errors, error => error.Contains("duplicado"));
        Assert.Contains(errors, error => error.Contains("suma de columnas"));
    }

    [Fact]
    public async Task ProposedContract_GeneratesPdfFromReferenceXml()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var contract = await File.ReadAllTextAsync(
            Path.Combine(fixturePath, "design-contract-btw-v3.0.json"));
        var xml = await File.ReadAllTextAsync(Path.Combine(fixturePath, "AM-12847.xml"));
        var runtime = new Dictionary<string, JsonElement>
        {
            ["Cufe"] = Json("\"test-cufe\""),
            ["QrImage"] = Json(
                "\"/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAQABADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD8qqKKKAP/2Q==\""),
            ["DianValidationDateTime"] = Json("\"2026-07-16T08:31:24-05:00\""),
            ["CurrentYear"] = Json("2026")
        };
        var images = new Mock<IImageRepository>();
        images.Setup(repository => repository.GetImageDataByKeyAsync(It.IsAny<string>()))
            .ReturnsAsync((byte[]?)null);
        var engine = new PdfRenderingEngine(images.Object, new ContractValidationService());

        var bytes = await engine.GeneratePdfAsync(
            contract,
            xml,
            "application/xml",
            runtime);

        using var stream = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.NotEmpty(pdf.Pages);
        Assert.InRange(pdf.Pages[0].Width.Millimeter, 215.8, 216.0);
    }

    private static PdfDesignSchema CreateSchema() => new()
    {
        SchemaVersion = "3.0",
        Document = new DocumentSettings { Id = "test", Name = "Test", Version = 1 },
        Page = new PageSettings
        {
            Size = "A4",
            WidthMm = 210,
            HeightMm = 297,
            MarginsMm = new Margins()
        },
        DataSource = new DataSourceSettings
        {
            RootPath = "/NewDataSet",
            Tables =
            [
                new DataTableDefinition
                {
                    Name = "Company",
                    DataPath = "Company",
                    Cardinality = "zeroOrOne"
                },
                new DataTableDefinition
                {
                    Name = "InvcDtl",
                    DataPath = "InvcDtl",
                    Cardinality = "zeroOrMany"
                }
            ],
            ComputedFields =
            [
                new ComputedFieldDefinition
                {
                    Name = "IssuerCheckDigit",
                    Resolver = new ResolverDefinition
                    {
                        Kind = "function",
                        Name = "colombianNitCheckDigit",
                        Arguments =
                        [
                            new BindingOperand { Kind = "path", Path = "Company.StateTaxID" }
                        ]
                    }
                }
            ]
        },
        Validation = new ContractValidationSettings { Status = "validated" }
    };

    private static JsonElement Json(string value) =>
        JsonDocument.Parse(value).RootElement.Clone();
}
