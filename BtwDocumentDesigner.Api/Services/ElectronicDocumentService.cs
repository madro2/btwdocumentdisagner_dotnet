using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BtwDocumentDesigner.Api.Services;

public sealed class ElectronicDocumentService : IElectronicDocumentService
{
    private static readonly Regex CufePattern =
        new("^[a-fA-F0-9]{32,128}$", RegexOptions.Compiled);
    private static readonly Regex XmlEncodingPattern =
        new(
            "encoding\\s*=\\s*['\"][^'\"]+['\"]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly IGeneratorService _generatorService;
    private readonly ElectronicDocumentSourceOptions _options;

    public ElectronicDocumentService(
        HttpClient httpClient,
        IGeneratorService generatorService,
        IOptions<ElectronicDocumentSourceOptions> options)
    {
        _httpClient = httpClient;
        _generatorService = generatorService;
        _options = options.Value;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<ElectronicDocumentXml> DownloadXmlAsync(
        string cufe,
        CancellationToken cancellationToken = default)
    {
        ValidateCufe(cufe);
        var relativePath = _options.XmlErpPathTemplate.Replace(
            "{cufe}",
            Uri.EscapeDataString(cufe),
            StringComparison.OrdinalIgnoreCase);
        using var response = await _httpClient.GetAsync(
            relativePath,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseBytes = await response.Content.ReadAsByteArrayAsync(
            cancellationToken);
        var sourceBytes = ExtractXmlBytes(responseBytes);
        var xml = NormalizeXmlEncoding(DecodeXml(sourceBytes));
        if (!xml.TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            throw new HttpRequestException(
                "El servicio de documentos no devolvió un XML ERP válido.");
        }

        var utf8Bytes = new UTF8Encoding(false).GetBytes(xml);
        return new ElectronicDocumentXml(
            cufe,
            $"{cufe}.xml",
            xml,
            Convert.ToBase64String(utf8Bytes));
    }

    public async Task<byte[]> GeneratePdfAsync(
        string cufe,
        string designName,
        int version,
        CancellationToken cancellationToken = default)
    {
        var document = await DownloadXmlAsync(cufe, cancellationToken);
        return await _generatorService.GeneratePdfAsync(
            designName,
            version,
            document.Xml,
            "application/xml; charset=utf-8");
    }

    private static void ValidateCufe(string cufe)
    {
        if (string.IsNullOrWhiteSpace(cufe) || !CufePattern.IsMatch(cufe))
        {
            throw new ArgumentException(
                "El CUFE debe contener entre 32 y 128 caracteres hexadecimales.",
                nameof(cufe));
        }
    }

    private static byte[] ExtractXmlBytes(byte[] responseBytes)
    {
        var text = Encoding.UTF8.GetString(responseBytes).Trim();
        if (text.StartsWith("<", StringComparison.Ordinal))
        {
            return responseBytes;
        }

        try
        {
            var node = JsonNode.Parse(text);
            var candidate = FindXmlPayload(node);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return DecodeBase64OrUtf8(candidate);
            }
        }
        catch (JsonException)
        {
            // La respuesta puede ser Base64 en texto plano.
        }

        return DecodeBase64OrUtf8(text);
    }

    private static string? FindXmlPayload(JsonNode? node)
    {
        if (
            node is JsonValue value
            && value.TryGetValue<string>(out var stringValue)
        )
        {
            return stringValue;
        }

        if (node is not JsonObject obj)
        {
            return null;
        }

        foreach (var propertyName in new[] { "base64", "data", "content", "xml", "file" })
        {
            if (
                obj[propertyName] is JsonValue property
                && property.TryGetValue<string>(out var candidate)
                && !string.IsNullOrWhiteSpace(candidate)
            )
            {
                return candidate;
            }
        }

        return FindXmlPayload(obj["result"]);
    }

    private static byte[] DecodeBase64OrUtf8(string value)
    {
        var normalized = value.Trim();
        try
        {
            return Convert.FromBase64String(normalized);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(normalized);
        }
    }

    private static string DecodeXml(byte[] bytes)
    {
        if (
            bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF
        )
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 256));
        var match = Regex.Match(
            header,
            "encoding\\s*=\\s*['\"](?<encoding>[^'\"]+)['\"]",
            RegexOptions.IgnoreCase);
        var encoding = match.Success
            ? Encoding.GetEncoding(match.Groups["encoding"].Value)
            : new UTF8Encoding(false, true);
        try
        {
            return encoding.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
        }
    }

    private static string NormalizeXmlEncoding(string xml)
    {
        return XmlEncodingPattern.IsMatch(xml)
            ? XmlEncodingPattern.Replace(xml, "encoding=\"utf-8\"", 1)
            : xml;
    }
}
