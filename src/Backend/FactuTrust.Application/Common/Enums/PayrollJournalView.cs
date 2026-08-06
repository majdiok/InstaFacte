namespace FactuTrust.Application.Common.Enums;

/// <summary>
/// Vue exportée du journal de paie. Le défaut est <see cref="ByEmployee"/> (état RH),
/// <see cref="Accounting"/> restituant la ventilation comptable OD du cycle.
/// </summary>
public enum PayrollJournalView
{
    ByEmployee = 0,
    Accounting = 1
}
