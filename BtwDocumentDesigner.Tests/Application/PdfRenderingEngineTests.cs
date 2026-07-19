using BtwDocumentDesigner.Application.Interfaces;
using BtwDocumentDesigner.Application.Rendering;
using Moq;

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
}
