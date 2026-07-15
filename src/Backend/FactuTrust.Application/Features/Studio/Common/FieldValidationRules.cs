namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// User-defined validation constraints for a custom field, serialized to
/// <c>CustomFieldDefinition.ValidationRulesJson</c>. All members optional.
/// </summary>
public sealed record FieldValidationRules
{
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }

    /// <summary>Regex the value must fully match (text fields only).</summary>
    public string? Regex { get; init; }

    /// <summary>Number of decimal places allowed (Decimal fields).</summary>
    public int? Decimals { get; init; }
}

/// <summary>A single choice for Select / MultiSelect fields.</summary>
public sealed record SelectOptionDto(string Value, string Label);

/// <summary>Relation target descriptor for Relation* fields.</summary>
public sealed record RelationRefDto(string Kind, string Ref);
