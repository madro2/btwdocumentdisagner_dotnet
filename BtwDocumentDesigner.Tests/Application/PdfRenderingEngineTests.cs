using BtwDocumentDesigner.Application.Interfaces;
using BtwDocumentDesigner.Application.Rendering;
using Moq;
using PdfSharp.Pdf.IO;

namespace BtwDocumentDesigner.Tests.Application;

public sealed class PdfRenderingEngineTests
{
    [Fact]
    public async Task GeneratePdfAsync_RendersTextWithCrossPlatformFont()
    {
        var images = new Mock<IImageRepository>();
        var systemDefaults = new Mock<ISystemDefaultValueRepository>();
        systemDefaults.Setup(x => x.GetAllAsync())
            .ReturnsAsync(new Dictionary<string, string>());
        var engine = new PdfRenderingEngine(images.Object, systemDefaults.Object);
        const string design = """
            {
              "page": {
                "widthMm": 210,
                "heightMm": 297,
                "orientation": "portrait",
                "background": "#ffffff"
              },
              "components": [
                {
                  "id": "title",
                  "type": "text",
                  "position": {
                    "x": 10,
                    "y": 10,
                    "width": 100,
                    "height": 10
                  },
                  "content": {
                    "value": "Factura electrónica {{InvcHead.LegalNum}}"
                  },
                  "style": {
                    "fontFamily": "Inter",
                    "fontSizePt": 12,
                    "color": "#111111",
                    "bold": true
                  }
                }
              ]
            }
            """;
        const string payload = """
            <Root>
              <InvcHead>
                <LegalNum>FE-123</LegalNum>
              </InvcHead>
            </Root>
            """;

        var result = await engine.GeneratePdfAsync(
            design,
            payload,
            "application/xml; charset=utf-8");

        Assert.True(result.Length > 100);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result, 0, 4));
    }

    [Fact]
    public async Task GeneratePdfAsync_RendersEachEntryInPagesArray()
    {
        var images = new Mock<IImageRepository>();
        var systemDefaults = new Mock<ISystemDefaultValueRepository>();
        systemDefaults.Setup(x => x.GetAllAsync()).ReturnsAsync(new Dictionary<string, string>());
        var engine = new PdfRenderingEngine(images.Object, systemDefaults.Object);
        const string design = """
            {
              "page": {
                "widthMm": 210,
                "heightMm": 297,
                "orientation": "portrait",
                "background": "#ffffff"
              },
              "components": [],
              "pages": [
                {
                  "id": "p1",
                  "page": {
                    "widthMm": 210,
                    "heightMm": 297,
                    "orientation": "portrait",
                    "background": "#ffffff"
                  },
                  "components": [
                    {
                      "id": "p1-text",
                      "type": "text",
                      "position": { "x": 10, "y": 10, "width": 80, "height": 10 },
                      "content": { "value": "Página {{Pagina.Actual}} de {{Pagina.Total}}" }
                    }
                  ]
                },
                {
                  "id": "p2",
                  "page": {
                    "widthMm": 297,
                    "heightMm": 210,
                    "orientation": "landscape",
                    "background": "#f8fafc"
                  },
                  "components": [
                    {
                      "id": "p2-text",
                      "type": "text",
                      "position": { "x": 10, "y": 10, "width": 80, "height": 10 },
                      "content": { "value": "Horizontal {{Pagina.Actual}}" }
                    }
                  ]
                }
              ]
            }
            """;

        var result = await engine.GeneratePdfAsync(
            design,
            "<Root />",
            "application/xml; charset=utf-8");

        using var stream = new MemoryStream(result);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.Equal(2, document.PageCount);
        Assert.True(document.Pages[0].Width.Millimeter < document.Pages[0].Height.Millimeter);
        Assert.True(document.Pages[1].Width.Millimeter > document.Pages[1].Height.Millimeter);
    }

    [Fact]
    public async Task GeneratePdfAsync_KeepsLegacySinglePageContract()
    {
        var images = new Mock<IImageRepository>();
        var systemDefaults = new Mock<ISystemDefaultValueRepository>();
        systemDefaults.Setup(x => x.GetAllAsync()).ReturnsAsync(new Dictionary<string, string>());
        var engine = new PdfRenderingEngine(images.Object, systemDefaults.Object);
        const string design = """
            {
              "page": {
                "widthMm": 210,
                "heightMm": 297,
                "orientation": "portrait",
                "background": "#ffffff"
              },
              "components": [
                {
                  "id": "only",
                  "type": "text",
                  "position": { "x": 5, "y": 5, "width": 40, "height": 8 },
                  "content": { "value": "Legacy" }
                }
              ]
            }
            """;

        var result = await engine.GeneratePdfAsync(
            design,
            "<Root />",
            "application/xml; charset=utf-8");

        using var stream = new MemoryStream(result);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public async Task GeneratePdfAsync_SupportsIncompleteDraftDesign()
    {
        var images = new Mock<IImageRepository>();
        var systemDefaults = new Mock<ISystemDefaultValueRepository>();
        systemDefaults.Setup(x => x.GetAllAsync()).ReturnsAsync(new Dictionary<string, string>());
        var engine = new PdfRenderingEngine(images.Object, systemDefaults.Object);

        // Contrato incompleto: sin page settings completas, componente sin style ni content definidos
        var draftDesign = """
            {
              "schemaVersion": "3.0",
              "document": { "id": "draft-1", "name": "Borrador Incompleto", "type": "document" },
              "components": [
                {
                  "id": "draft-text",
                  "type": "text",
                  "position": { "x": 10, "y": 10, "width": 50, "height": 10 }
                }
              ]
            }
            """;

        var result = await engine.GeneratePdfAsync(
            draftDesign,
            "{}",
            "application/json");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        using var stream = new MemoryStream(result);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public async Task GeneratePdfAsync_RendersLinkAndBarcodeCorrectly()
    {
        var images = new Mock<IImageRepository>();
        var systemDefaults = new Mock<ISystemDefaultValueRepository>();
        systemDefaults.Setup(x => x.GetAllAsync()).ReturnsAsync(new Dictionary<string, string>());
        var engine = new PdfRenderingEngine(images.Object, systemDefaults.Object);

        var designWithLinkAndBarcode = """
            {
              "schemaVersion": "3.0",
              "document": { "id": "test-lb", "name": "Test Link y Barcode", "type": "document" },
              "components": [
                {
                  "id": "link-1",
                  "type": "link",
                  "position": { "x": 10, "y": 10, "width": 60, "height": 10 },
                  "content": { "value": "Visitar BTW" },
                  "style": { "italic": true }
                },
                {
                  "id": "barcode-1",
                  "type": "barcode",
                  "position": { "x": 10, "y": 25, "width": 70, "height": 20 },
                  "content": { "value": "7701234567890" }
                }
              ]
            }
            """;

        var result = await engine.GeneratePdfAsync(
            designWithLinkAndBarcode,
            "{}",
            "application/json");

        Assert.NotNull(result);
        Assert.True(result.Length > 200);
        using var streamLink = new MemoryStream(result);
        using var docLink = PdfReader.Open(streamLink, PdfDocumentOpenMode.Import);
        Assert.Equal(1, docLink.PageCount);
    }
    [Fact]
    public async Task GeneratePdfAsync_RendersCustomizedTableWithBorderPresetAndStriping()
    {
        var images = new Mock<IImageRepository>();
        var systemDefaults = new Mock<ISystemDefaultValueRepository>();
        systemDefaults.Setup(x => x.GetAllAsync()).ReturnsAsync(new Dictionary<string, string>());
        var engine = new PdfRenderingEngine(images.Object, systemDefaults.Object);

        var designWithTable = """
            {
              "schemaVersion": "3.0",
              "document": { "id": "test-tbl", "name": "Test Tabla Personalizada", "type": "document" },
              "components": [
                {
                  "id": "table-1",
                  "type": "table",
                  "position": { "x": 10, "y": 10, "width": 190, "height": 60 },
                  "style": {
                    "borderPreset": "horizontal",
                    "alternateRowBackground": "#f8fafc",
                    "cellPaddingMm": 1.5,
                    "border": { "style": "solid", "widthPt": 0.75, "color": "#cbd5e1" },
                    "header": { "background": "#1e293b", "bold": true, "alignment": "center" }
                  },
                  "content": {
                    "mode": "collection",
                    "dataPath": "Items",
                    "rowAlias": "Item"
                  },
                  "columns": [
                    { "id": "col1", "title": "Código", "widthMm": 30, "dataPath": "Item.Code", "alignment": "center" },
                    { "id": "col2", "title": "Descripción", "widthMm": 110, "dataPath": "Item.Desc", "alignment": "left" },
                    { "id": "col3", "title": "Total", "widthMm": 50, "dataPath": "Item.Total", "alignment": "right" }
                  ]
                }
              ]
            }
            """;

        var sampleJson = """
            {
              "Items": [
                { "Code": "A001", "Desc": "Producto A", "Total": "$10,000" },
                { "Code": "A002", "Desc": "Producto B", "Total": "$25,000" },
                { "Code": "A003", "Desc": "Producto C", "Total": "$15,000" }
              ]
            }
            """;

        var result = await engine.GeneratePdfAsync(
            designWithTable,
            sampleJson,
            "application/json");

        Assert.NotNull(result);
        Assert.True(result.Length > 500);
        using var stream = new MemoryStream(result);
        using var doc = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
    }
}
