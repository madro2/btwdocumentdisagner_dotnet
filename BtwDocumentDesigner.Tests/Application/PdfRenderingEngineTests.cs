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
}
