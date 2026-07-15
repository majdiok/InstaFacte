namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// A form layout: ordered sections, each with ordered field references. Serialized to
/// <c>CustomFormDefinition.LayoutJson</c>. The default form (one untitled section listing all
/// active fields) is generated when none is saved.
/// </summary>
public sealed record FormLayout
{
    public IReadOnlyList<FormSection> Sections { get; init; } = Array.Empty<FormSection>();
}

public sealed record FormSection
{
    public string? Title { get; init; }
    public IReadOnlyList<FormFieldRef> Fields { get; init; } = Array.Empty<FormFieldRef>();
}

public sealed record FormFieldRef
{
    public string Key { get; init; } = string.Empty;
    public string? LabelOverride { get; init; }

    /// <summary><c>full</c> or <c>half</c>; defaults to full.</summary>
    public string? Width { get; init; }
}

// ---- Form API DTOs ----

public sealed record CustomFormDto(
    Guid Id,
    string Key,
    string DisplayName,
    bool IsDefault,
    FormLayout Layout);

public sealed record SaveFormLayoutRequest(FormLayout Layout, string? DisplayName);
