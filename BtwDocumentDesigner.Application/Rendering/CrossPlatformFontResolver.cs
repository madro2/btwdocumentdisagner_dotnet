using PdfSharp.Fonts;

namespace BtwDocumentDesigner.Application.Rendering
{
    public sealed class CrossPlatformFontResolver : IFontResolver
    {
        private const string RegularFace = "btw-sans-regular";
        private const string BoldFace = "btw-sans-bold";
        private const string ItalicFace = "btw-sans-italic";
        private const string BoldItalicFace = "btw-sans-bold-italic";

        private static readonly IReadOnlyDictionary<string, string[]> FontFileNames =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [RegularFace] =
                [
                    "arial.ttf",
                    "DejaVuSans.ttf",
                    "LiberationSans-Regular.ttf"
                ],
                [BoldFace] =
                [
                    "arialbd.ttf",
                    "DejaVuSans-Bold.ttf",
                    "LiberationSans-Bold.ttf"
                ],
                [ItalicFace] =
                [
                    "ariali.ttf",
                    "DejaVuSans-Oblique.ttf",
                    "LiberationSans-Italic.ttf"
                ],
                [BoldItalicFace] =
                [
                    "arialbi.ttf",
                    "DejaVuSans-BoldOblique.ttf",
                    "LiberationSans-BoldItalic.ttf"
                ]
            };

        private static readonly string[] FontDirectories =
        [
            Path.Combine(AppContext.BaseDirectory, "Fonts"),
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            "/usr/share/fonts/truetype/dejavu",
            "/usr/share/fonts/truetype/liberation2",
            "/usr/share/fonts/truetype/liberation",
            "/usr/local/share/fonts"
        ];

        private static readonly Dictionary<string, byte[]> Cache =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lock CacheLock = new();

        public string DefaultFontName => "BTW Sans";

        public byte[] GetFont(string faceName)
        {
            lock (CacheLock)
            {
                if (Cache.TryGetValue(faceName, out var cached))
                {
                    return cached;
                }

                if (!FontFileNames.TryGetValue(faceName, out var fileNames))
                {
                    throw new InvalidOperationException(
                        $"PDFsharp solicitó una variante de fuente desconocida: {faceName}.");
                }

                foreach (var directory in FontDirectories.Where(
                    directory => !string.IsNullOrWhiteSpace(directory)))
                {
                    foreach (var fileName in fileNames)
                    {
                        var path = Path.Combine(directory, fileName);
                        if (!File.Exists(path))
                        {
                            continue;
                        }

                        var font = File.ReadAllBytes(path);
                        if (font.Length > 0)
                        {
                            Cache[faceName] = font;
                            return font;
                        }
                    }
                }

                throw new FileNotFoundException(
                    "No se encontró una fuente TrueType compatible. "
                    + "Instale fonts-dejavu-core en Linux o incluya las fuentes "
                    + "en la carpeta Fonts de la aplicación.");
            }
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var faceName = (isBold, isItalic) switch
            {
                (true, true) => BoldItalicFace,
                (true, false) => BoldFace,
                (false, true) => ItalicFace,
                _ => RegularFace
            };
            return new FontResolverInfo(faceName);
        }
    }
}
