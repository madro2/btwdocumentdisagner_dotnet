namespace BtwDocumentDesigner.Application.Rendering;

public sealed class ContractValidationException : Exception
{
    public ContractValidationException(IEnumerable<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed class ContractValidationService
{
    private static readonly HashSet<string> SupportedVersions = ["2.1", "2.2", "3.0"];
    private static readonly HashSet<string> SupportedTypes =
    [
        "text", "image", "line", "rectangle", "table", "qrCode", "barcode",
        "container", "pageBreak", "pageNumber", "link"
    ];

    public void ValidateAndThrow(PdfDesignSchema schema)
    {
        var errors = Validate(schema);
        if (errors.Count > 0) throw new ContractValidationException(errors);
    }

    public IReadOnlyList<string> Validate(PdfDesignSchema schema)
    {
        var errors = new List<string>();
        if (!SupportedVersions.Contains(schema.SchemaVersion))
            errors.Add($"schemaVersion '{schema.SchemaVersion}' no está soportado.");
        if (schema.Page.WidthMm <= 0 || schema.Page.HeightMm <= 0)
            errors.Add("Las dimensiones de page deben ser mayores que cero.");
        if (schema.Components.Count == 0)
            errors.Add("components debe contener al menos un componente.");
        if (schema.SchemaVersion == "3.0" && schema.Validation.PendingBindings.Count > 0)
            errors.Add("Un contrato 3.0 ejecutable no puede contener pendingBindings.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var resources = schema.Resources.Select(resource => resource.Id)
            .ToHashSet(StringComparer.Ordinal);
        ValidateComponents(
            schema.Components,
            schema.Page.WidthMm,
            schema.Page.HeightMm,
            ids,
            resources,
            errors);
        return errors;
    }

    private static void ValidateComponents(
        IEnumerable<PdfComponent> components,
        double parentWidth,
        double parentHeight,
        HashSet<string> ids,
        HashSet<string> resources,
        List<string> errors)
    {
        foreach (var component in components)
        {
            if (string.IsNullOrWhiteSpace(component.Id))
                errors.Add("Todos los componentes deben tener id.");
            else if (!ids.Add(component.Id))
                errors.Add($"El id de componente '{component.Id}' está duplicado.");

            if (!SupportedTypes.Contains(component.Type))
                errors.Add($"El componente '{component.Id}' usa el tipo no soportado '{component.Type}'.");

            var position = component.Position;
            if (component.Type != "pageBreak" &&
                (position.X < 0 || position.Y < 0 ||
                 position.X + position.Width > parentWidth + 0.01 ||
                 position.Y + position.Height > parentHeight + 0.01))
            {
                errors.Add($"El componente '{component.Id}' está fuera de los límites de su padre.");
            }

            if (component.Type == "table" && component.Columns.Count > 0)
            {
                var totalWidth = component.Columns.Sum(column => column.WidthMm ?? 0);
                if (Math.Abs(totalWidth - position.Width) > 0.1)
                    errors.Add(
                        $"La suma de columnas de '{component.Id}' ({totalWidth:0.###} mm) " +
                        $"no coincide con su ancho ({position.Width:0.###} mm).");
            }

            if (component.Content.Source == "asset" &&
                !string.IsNullOrWhiteSpace(component.Content.AssetId) &&
                !resources.Contains(component.Content.AssetId))
            {
                errors.Add(
                    $"El componente '{component.Id}' referencia el recurso inexistente " +
                    $"'{component.Content.AssetId}'.");
            }

            ValidateComponents(
                component.Components,
                position.Width,
                position.Height,
                ids,
                resources,
                errors);
        }
    }
}
