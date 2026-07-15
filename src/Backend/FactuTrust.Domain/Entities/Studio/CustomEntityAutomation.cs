using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// An "ERP bridge" automation attached to a custom entity: when its <see cref="Trigger"/> fires, the
/// mapped ERP <see cref="ActionKey"/> (an AI-tool / validated command) is executed with arguments built
/// from the record's fields (<see cref="MappingJson"/>). Purely declarative — execution reuses the
/// existing, permission-gated tool executor. Calquée sur <c>CustomViewDefinition</c>.
/// </summary>
public sealed class CustomEntityAutomation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>The custom entity this automation belongs to.</summary>
    public Guid EntityDefinitionId { get; private set; }

    public string Name { get; private set; } = null!;

    public StudioAutomationTrigger Trigger { get; private set; }

    /// <summary>Name of the ERP action to run (an AI tool / validated command, e.g. <c>generate_invoice</c>).</summary>
    public string ActionKey { get; private set; } = null!;

    /// <summary>Serialized parameter mapping: <c>[{ param, source:'field'|'relation'|'const', value }]</c>.</summary>
    public string MappingJson { get; private set; } = null!;

    /// <summary>Optional serialized condition (filter) that must hold for the action to run.</summary>
    public string? ConditionJson { get; private set; }

    /// <summary>When true, the action runs at most once per record (idempotent via the run log).</summary>
    public bool RunOnce { get; private set; }

    /// <summary>Optional serialized recurrence config — reserved for the scheduled phase.</summary>
    public string? ScheduleJson { get; private set; }

    public bool IsActive { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomEntityAutomation() { }

    public static CustomEntityAutomation Create(
        Guid tenantId, Guid entityDefinitionId, string name, StudioAutomationTrigger trigger,
        string actionKey, string mappingJson, string? conditionJson, bool runOnce, string? scheduleJson, Guid? createdBy)
    {
        return new CustomEntityAutomation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityDefinitionId = entityDefinitionId,
            Name = name,
            Trigger = trigger,
            ActionKey = actionKey,
            MappingJson = mappingJson,
            ConditionJson = conditionJson,
            RunOnce = runOnce,
            ScheduleJson = scheduleJson,
            IsActive = true,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name, StudioAutomationTrigger trigger, string actionKey, string mappingJson,
        string? conditionJson, bool runOnce, string? scheduleJson, bool isActive, Guid? updatedBy)
    {
        Name = name;
        Trigger = trigger;
        ActionKey = actionKey;
        MappingJson = mappingJson;
        ConditionJson = conditionJson;
        RunOnce = runOnce;
        ScheduleJson = scheduleJson;
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
