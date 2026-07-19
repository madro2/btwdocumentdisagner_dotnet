using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BtwDocumentDesigner.Application.Rendering;

public sealed record BindingScope(
    XElement? Row = null,
    string? RowAlias = null,
    IReadOnlyDictionary<string, object?>? LocalValues = null);

public sealed class BindingResolver
{
    private static readonly Regex TokenPattern =
        new(@"\{\{(.+?)\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly PdfDesignSchema _schema;
    private readonly XDocument? _xml;
    private readonly JObject? _json;
    private readonly IReadOnlyDictionary<string, JsonElement> _runtime;
    private readonly IReadOnlyDictionary<string, object?> _renderContext;

    public BindingResolver(
        PdfDesignSchema schema,
        XDocument? xml,
        JObject? json,
        IReadOnlyDictionary<string, JsonElement>? runtime = null,
        IReadOnlyDictionary<string, object?>? renderContext = null)
    {
        _schema = schema;
        _xml = xml;
        _json = json;
        _runtime = runtime ?? new Dictionary<string, JsonElement>();
        _renderContext = renderContext ?? new Dictionary<string, object?>();
    }

    public string Render(
        string? template,
        BindingScope? scope = null,
        IReadOnlyDictionary<string, BindingDefinition>? localBindings = null,
        string? defaultValue = null)
    {
        if (string.IsNullOrEmpty(template)) return defaultValue ?? string.Empty;
        scope ??= new BindingScope();

        return TokenPattern.Replace(template, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            var value = EvaluateExpression(expression, scope, localBindings);
            return IsEmpty(value) ? defaultValue ?? string.Empty : ToText(value);
        });
    }

    public object? Resolve(string path, BindingScope? scope = null)
    {
        scope ??= new BindingScope();
        if (string.IsNullOrWhiteSpace(path)) return null;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        if (scope.LocalValues?.TryGetValue(path, out var local) == true) return local;
        if (segments[0] == "Runtime")
            return _runtime.TryGetValue(string.Join('.', segments.Skip(1)), out var runtime)
                ? JsonValue(runtime)
                : null;
        if (segments[0] is "Pagina" or "Table" or "Iteration" or "System")
            return _renderContext.TryGetValue(path, out var renderValue) ? renderValue : null;
        if (segments[0] == "Computed")
            return ResolveComputed(string.Join('.', segments.Skip(1)), scope);
        if (scope.Row is not null && segments[0] == scope.RowAlias)
            return ReadXmlPath(scope.Row, segments.Skip(1));

        if (_xml is not null) return ResolveXml(segments);
        if (_json is not null)
        {
            var token = _json.SelectToken("$." + path);
            if (token is null) return null;
            return token is JValue value ? value.Value : token.ToString();
        }
        return null;
    }

    public IReadOnlyList<XElement> Collection(string path)
    {
        if (_xml is null) return [];
        var root = DataRoot();
        var table = _schema.DataSource.Tables.FirstOrDefault(
            item => item.Name.Equals(path, StringComparison.Ordinal));
        var elementName = table?.DataPath ?? path;
        return root.Elements().Where(
            element => element.Name.LocalName.Equals(elementName, StringComparison.Ordinal)).ToArray();
    }

    public void ValidateRuntime()
    {
        var missing = _schema.DataSource.RuntimeParameters
            .Where(parameter => parameter.Required)
            .Where(parameter => !_runtime.ContainsKey(parameter.Name))
            .Select(parameter => $"Runtime.{parameter.Name}")
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(
                $"Faltan parámetros runtime obligatorios: {string.Join(", ", missing)}.");
    }

    private object? EvaluateExpression(
        string expression,
        BindingScope scope,
        IReadOnlyDictionary<string, BindingDefinition>? localBindings)
    {
        var parts = expression.Split('|', StringSplitOptions.TrimEntries);
        object? value;
        if (localBindings?.TryGetValue(parts[0], out var binding) == true)
            value = EvaluateBinding(binding, scope);
        else
            value = Resolve(parts[0], scope);

        foreach (var filterExpression in parts.Skip(1))
            value = ApplyFilter(filterExpression, value, scope);
        return value;
    }

    private object? ApplyFilter(string expression, object? value, BindingScope scope)
    {
        var separator = expression.IndexOf(':');
        var name = separator < 0 ? expression : expression[..separator];
        var arguments = separator < 0 ? string.Empty : expression[(separator + 1)..];
        switch (name)
        {
            case "upper":
                return ToText(value).ToUpperInvariant();
            case "number":
            {
                var decimals = int.TryParse(arguments, out var parsed) ? parsed : 0;
                return AsDecimal(value).ToString(
                    $"N{decimals}", CultureInfo.GetCultureInfo("en-US"));
            }
            case "currency":
            {
                var currencyCode = ToText(Resolve(arguments, scope));
                var symbol = currencyCode == "COP" ? "$" : currencyCode;
                return $"{symbol} {AsDecimal(value).ToString("N2", CultureInfo.GetCultureInfo("en-US"))}";
            }
            case "date":
                return DateTimeOffset.TryParse(
                    ToText(value), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                    ? date.ToString(arguments, CultureInfo.InvariantCulture)
                    : value;
            case "lookup":
                return _schema.DataSource.Lookups.TryGetValue(arguments, out var lookup) &&
                       lookup.TryGetValue(ToText(value), out var mapped)
                    ? mapped
                    : value;
            case "percentageOf":
            {
                var path = arguments.Split(':', 2)[0];
                var denominator = AsDecimal(Resolve(path, scope));
                return denominator == 0 ? 0 : AsDecimal(value) / denominator * 100;
            }
            default:
                throw new InvalidOperationException($"Filtro de binding no soportado: '{name}'.");
        }
    }

    private object? EvaluateBinding(BindingDefinition binding, BindingScope scope)
    {
        if (binding.Source == "path") return Resolve(binding.DataPath ?? string.Empty, scope);
        if (binding.Source != "function") return JsonValue(binding.DefaultValue);
        var arguments = binding.Arguments.Select(argument => Operand(argument, scope)).ToArray();
        return binding.Function switch
        {
            "amountToWords" => SpanishNumberWords.ToWords(
                AsDecimal(arguments.ElementAtOrDefault(0)),
                ToText(arguments.ElementAtOrDefault(1))),
            "replace" => ToText(arguments.ElementAtOrDefault(0)).Replace(
                ToText(arguments.ElementAtOrDefault(1)),
                ToText(arguments.ElementAtOrDefault(2)),
                StringComparison.Ordinal),
            _ => JsonValue(binding.DefaultValue)
        };
    }

    private object? ResolveComputed(string name, BindingScope scope)
    {
        var computed = _schema.DataSource.ComputedFields.FirstOrDefault(
            field => field.Name.Equals(name, StringComparison.Ordinal));
        if (computed is null) return null;
        var resolver = computed.Resolver;

        if (resolver.Kind == "lookup")
        {
            var key = Operand(resolver.Key, scope);
            return resolver.Lookup is not null &&
                   _schema.DataSource.Lookups.TryGetValue(resolver.Lookup, out var lookup) &&
                   lookup.TryGetValue(ToText(key), out var mapped)
                ? mapped
                : JsonValue(resolver.DefaultValue);
        }

        var arguments = resolver.Arguments.Select(argument => Operand(argument, scope)).ToArray();
        return resolver.Kind switch
        {
            "coalesce" => arguments.FirstOrDefault(value => !IsEmpty(value)),
            "function" when resolver.Name == "replace" =>
                ToText(arguments.ElementAtOrDefault(0)).Replace(
                    ToText(arguments.ElementAtOrDefault(1)),
                    ToText(arguments.ElementAtOrDefault(2)),
                    StringComparison.Ordinal),
            "function" when resolver.Name == "colombianNitCheckDigit" =>
                ColombianNitCheckDigit(ToText(arguments.ElementAtOrDefault(0))),
            _ => JsonValue(resolver.DefaultValue)
        };
    }

    private object? Operand(BindingOperand? operand, BindingScope scope)
    {
        if (operand is null) return null;
        return operand.Kind == "path"
            ? Resolve(operand.Path ?? string.Empty, scope)
            : JsonValue(operand.Value);
    }

    private object? ResolveXml(string[] segments)
    {
        var table = _schema.DataSource.Tables.FirstOrDefault(
            item => item.Name.Equals(segments[0], StringComparison.Ordinal));
        var recordName = table?.DataPath ?? segments[0];
        var record = DataRoot().Elements().FirstOrDefault(
            element => element.Name.LocalName.Equals(recordName, StringComparison.Ordinal));
        return record is null ? null : ReadXmlPath(record, segments.Skip(1));
    }

    private XElement DataRoot()
    {
        var root = _xml?.Root
                   ?? throw new InvalidOperationException("El XML no contiene un elemento raíz.");
        var expected = _schema.DataSource.RootPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (expected is null || root.Name.LocalName == expected) return root;
        return _xml.Descendants().FirstOrDefault(element => element.Name.LocalName == expected)
               ?? root;
    }

    private static string? ReadXmlPath(XElement start, IEnumerable<string> segments)
    {
        XElement? current = start;
        foreach (var segment in segments)
            current = current?.Elements().FirstOrDefault(
                element => element.Name.LocalName.Equals(segment, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(current?.Value) ? null : current.Value.Trim();
    }

    private static int ColombianNitCheckDigit(string value)
    {
        var digits = value.Where(char.IsDigit).Select(character => character - '0').ToArray();
        int[] weights = [71, 67, 59, 53, 47, 43, 41, 37, 29, 23, 19, 17, 13, 7, 3];
        if (digits.Length > weights.Length) return 0;
        var offset = weights.Length - digits.Length;
        var sum = digits.Select((digit, index) => digit * weights[index + offset]).Sum();
        var remainder = sum % 11;
        return remainder < 2 ? remainder : 11 - remainder;
    }

    private static decimal AsDecimal(object? value) =>
        decimal.TryParse(
            ToText(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static bool IsEmpty(object? value) =>
        value is null || string.IsNullOrWhiteSpace(ToText(value));

    private static string ToText(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static object? JsonValue(JsonElement? element)
    {
        if (element is null) return null;
        return element.Value.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number when element.Value.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.Value.GetRawText()
        };
    }
}

internal static class SpanishNumberWords
{
    private static readonly string[] Units =
    [
        "CERO", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO",
        "NUEVE", "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS",
        "DIECISIETE", "DIECIOCHO", "DIECINUEVE", "VEINTE", "VEINTIUNO", "VEINTIDÓS",
        "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE",
        "VEINTIOCHO", "VEINTINUEVE"
    ];

    private static readonly string[] Tens =
        ["", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];
    private static readonly string[] Hundreds =
        ["", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
         "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"];

    public static string ToWords(decimal value, string currency)
    {
        var integer = (long)Math.Truncate(Math.Abs(value));
        var words = Integer(integer);
        var currencyName = currency == "COP" ? "PESOS" : currency;
        return $"{words} {currencyName}".Trim();
    }

    private static string Integer(long value)
    {
        if (value < 30) return Units[value];
        if (value < 100)
        {
            var unit = value % 10;
            return unit == 0 ? Tens[value / 10] : $"{Tens[value / 10]} Y {Units[unit]}";
        }
        if (value == 100) return "CIEN";
        if (value < 1000) return $"{Hundreds[value / 100]} {Integer(value % 100)}".Trim();
        if (value < 1_000_000)
        {
            var thousands = value / 1000;
            var prefix = thousands == 1 ? "MIL" : $"{Integer(thousands)} MIL";
            return $"{prefix} {(value % 1000 == 0 ? "" : Integer(value % 1000))}".Trim();
        }
        if (value < 1_000_000_000)
        {
            var millions = value / 1_000_000;
            var prefix = millions == 1 ? "UN MILLÓN" : $"{Integer(millions)} MILLONES";
            return $"{prefix} {(value % 1_000_000 == 0 ? "" : Integer(value % 1_000_000))}".Trim();
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
