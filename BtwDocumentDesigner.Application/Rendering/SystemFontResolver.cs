using PdfSharp.Fonts;

namespace BtwDocumentDesigner.Application.Rendering;

public sealed class SystemFontResolver : IFontResolver
{
    private const string Regular = "btw-regular";
    private const string Bold = "btw-bold";
    private const string Italic = "btw-italic";
    private const string BoldItalic = "btw-bold-italic";
    private readonly Dictionary<string, string> _fontFiles;

    public SystemFontResolver()
    {
        _fontFiles = LocateFonts();
    }

    public FontResolverInfo ResolveTypeface(
        string familyName,
        bool isBold,
        bool isItalic) =>
        new((isBold, isItalic) switch
        {
            (true, true) => BoldItalic,
            (true, false) => Bold,
            (false, true) => Italic,
            _ => Regular
        });

    public byte[] GetFont(string faceName) =>
        File.ReadAllBytes(_fontFiles.TryGetValue(faceName, out var file)
            ? file
            : _fontFiles[Regular]);

    private static Dictionary<string, string> LocateFonts()
    {
        var candidates = new[]
        {
            new
            {
                Regular = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    "arial.ttf"),
                Bold = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    "arialbd.ttf"),
                Italic = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    "ariali.ttf"),
                BoldItalic = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    "arialbi.ttf")
            },
            new
            {
                Regular = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                Bold = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                Italic = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf",
                BoldItalic = "/usr/share/fonts/truetype/dejavu/DejaVuSans-BoldOblique.ttf"
            }
        };
        var selected = candidates.FirstOrDefault(candidate => File.Exists(candidate.Regular))
                       ?? throw new InvalidOperationException(
                           "No se encontró una fuente Arial o DejaVu Sans para generar el PDF.");
        return new Dictionary<string, string>
        {
            [Regular] = selected.Regular,
            [Bold] = File.Exists(selected.Bold) ? selected.Bold : selected.Regular,
            [Italic] = File.Exists(selected.Italic) ? selected.Italic : selected.Regular,
            [BoldItalic] = File.Exists(selected.BoldItalic) ? selected.BoldItalic : selected.Regular
        };
    }
}
