using System.Text.Json;
using FactuTrust.Domain.Entities.Studio;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>(De)serialization + sanitization for form layouts.</summary>
public static class FormLayoutJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(FormLayout layout) => JsonSerializer.Serialize(layout, Options);

    public static FormLayout Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new FormLayout();
        try { return JsonSerializer.Deserialize<FormLayout>(json, Options) ?? new FormLayout(); }
        catch (JsonException) { return new FormLayout(); }
    }

    /// <summary>The implicit default: one untitled section listing all active fields in sort order.</summary>
    public static FormLayout BuildDefault(IReadOnlyList<CustomFieldDefinition> fields)
    {
        var refs = fields
            .OrderBy(f => f.SortOrder)
            .Select(f => new FormFieldRef { Key = f.Key, Width = "full" })
            .ToList();
        return new FormLayout { Sections = new[] { new FormSection { Fields = refs } } };
    }

    /// <summary>
    /// Keeps only field refs whose key matches an active field; drops empty sections.
    /// Guards against stale keys after a field is removed/renamed.
    /// </summary>
    public static FormLayout SanitizeAgainstFields(FormLayout layout, IReadOnlyList<CustomFieldDefinition> fields)
    {
        var valid = fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var sections = new List<FormSection>();
        foreach (var section in layout.Sections)
        {
            var keptFields = section.Fields
                .Where(fr => valid.Contains(fr.Key))
                .Select(fr => new FormFieldRef
                {
                    Key = fr.Key,
                    LabelOverride = string.IsNullOrWhiteSpace(fr.LabelOverride) ? null : fr.LabelOverride.Trim(),
                    Width = fr.Width == "half" ? "half" : "full"
                })
                .ToList();
            if (keptFields.Count > 0)
                sections.Add(new FormSection { Title = string.IsNullOrWhiteSpace(section.Title) ? null : section.Title.Trim(), Fields = keptFields });
        }
        return new FormLayout { Sections = sections };
    }
}
