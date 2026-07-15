using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// A saved report/view definition. Its data source is either a custom entity or a whitelisted
/// existing (read-only) source; <see cref="DefinitionJson"/> stores the selected fields, filters,
/// grouping, aggregations, and sort. Execution is always read-only.
/// </summary>
public sealed class CustomReportDefinition
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Unique per tenant.</summary>
    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public CustomReportDataSourceKind DataSourceKind { get; private set; }

    /// <summary>The <c>CustomEntityDefinition.Key</c> (custom) or whitelisted source key (existing).</summary>
    public string DataSourceRef { get; private set; } = null!;

    /// <summary>Serialized report definition: selectedFields, filters, grouping, aggregations, sort.</summary>
    public string DefinitionJson { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomReportDefinition() { }

    public static CustomReportDefinition Create(
        Guid tenantId,
        string key,
        string displayName,
        CustomReportDataSourceKind dataSourceKind,
        string dataSourceRef,
        string definitionJson,
        Guid? createdBy)
    {
        return new CustomReportDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            DisplayName = displayName,
            DataSourceKind = dataSourceKind,
            DataSourceRef = dataSourceRef,
            DefinitionJson = definitionJson,
            IsActive = true,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string displayName,
        CustomReportDataSourceKind dataSourceKind,
        string dataSourceRef,
        string definitionJson,
        bool isActive,
        Guid? updatedBy)
    {
        DisplayName = displayName;
        DataSourceKind = dataSourceKind;
        DataSourceRef = dataSourceRef;
        DefinitionJson = definitionJson;
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
