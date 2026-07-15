using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

public sealed class FiscalScheduleHistoryEntry : Entity
{
    public Guid FiscalScheduleEntryId { get; private set; }
    public FiscalScheduleEntry FiscalScheduleEntry { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Summary { get; private set; } = null!;
    public string? OldValuesJson { get; private set; }
    public string? NewValuesJson { get; private set; }

    private FiscalScheduleHistoryEntry() { }

    public static FiscalScheduleHistoryEntry Create(
        Guid fiscalScheduleEntryId,
        string action,
        string summary,
        string? oldValuesJson = null,
        string? newValuesJson = null)
    {
        return new FiscalScheduleHistoryEntry
        {
            FiscalScheduleEntryId = fiscalScheduleEntryId,
            Action = string.IsNullOrWhiteSpace(action) ? "Updated" : action.Trim(),
            Summary = string.IsNullOrWhiteSpace(summary) ? "Modification de l'echeance fiscale" : summary.Trim(),
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson
        };
    }
}
