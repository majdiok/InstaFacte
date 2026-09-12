using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// Vue enregistrée sur les enregistrements d'une table Studio (<see cref="CustomEntityDefinition"/>) :
/// liste, kanban ou calendrier. La définition (colonnes, filtres, tri, options kanban/calendrier) est
/// stockée en JSON et re-validée contre les champs actifs à chaque enregistrement et à chaque exécution.
/// Une seule vue par table peut être la vue par défaut ; la suppression est logique (<see cref="IsDeleted"/>).
/// À ne pas confondre avec <see cref="CustomViewDefinition"/> (fenêtre SQL en lecture seule sur une
/// table existante de l'ERP).
/// </summary>
public sealed class CustomRecordViewDefinition
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EntityDefinitionId { get; private set; }

    /// <summary>Clé machine, unique par (tenant, table) parmi les vues non supprimées.</summary>
    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public CustomRecordViewMode Mode { get; private set; }

    /// <summary>Sérialisation camelCase de <c>RecordViewDefinition</c> (colonnes, filtres, tri, kanban, calendrier, pageSize).</summary>
    public string DefinitionJson { get; private set; } = null!;

    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomRecordViewDefinition() { }

    public static CustomRecordViewDefinition Create(
        Guid tenantId,
        Guid entityDefinitionId,
        string key,
        string displayName,
        CustomRecordViewMode mode,
        string definitionJson,
        bool isDefault,
        Guid? createdBy)
    {
        return new CustomRecordViewDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityDefinitionId = entityDefinitionId,
            Key = key,
            DisplayName = displayName,
            Mode = mode,
            DefinitionJson = definitionJson,
            IsDefault = isDefault,
            IsActive = true,
            IsDeleted = false,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string displayName, CustomRecordViewMode mode, string definitionJson, bool isActive, Guid? updatedBy)
    {
        DisplayName = displayName;
        Mode = mode;
        DefinitionJson = definitionJson;
        IsActive = isActive;
        Touch(updatedBy);
    }

    public void SetDefault(bool isDefault, Guid? updatedBy)
    {
        IsDefault = isDefault;
        Touch(updatedBy);
    }

    public void SoftDelete(Guid? deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        // R12 : aucune promotion automatique d'une autre vue ; la vue supprimée cesse simplement d'être la vue par défaut.
        IsDefault = false;
        Touch(deletedBy);
    }

    private void Touch(Guid? updatedBy)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
