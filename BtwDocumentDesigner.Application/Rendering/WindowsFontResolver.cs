using PdfSharp.Fonts;
using System;
using System.IO;

namespace BtwDocumentDesigner.Application.Rendering
{
    public class WindowsFontResolver : IFontResolver
    {
        public string DefaultFontName => "Arial";

        public byte[] GetFont(string faceName)
        {
            var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), faceName + ".ttf");
            if (File.Exists(fontPath)) return File.ReadAllBytes(fontPath);

            var arialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            return File.Exists(arialPath) ? File.ReadAllBytes(arialPath) : Array.Empty<byte>();
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            string faceName = familyName.ToLower();
            if (faceName == "arial")
            {
                if (isBold && isItalic) faceName = "arialbi";
                else if (isBold) faceName = "arialbd";
                else if (isItalic) faceName = "ariali";
            }
            return new FontResolverInfo(faceName);
        }
    }
}