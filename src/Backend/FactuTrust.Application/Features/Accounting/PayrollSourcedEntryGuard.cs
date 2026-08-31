namespace FactuTrust.Application.Features.Accounting;

/// <summary>
/// Garde-fou partagé des écritures générées par le module Paie : une écriture dont
/// <see cref="Domain.Entities.JournalEntry.SourceEntityType"/> est une source paie ne peut être
/// extournée ni supprimée depuis la comptabilité — elle se corrige uniquement via le workflow paie
/// (« Rouvrir le cycle », « Annuler le paiement »). L'autorité reste le backend ; l'UI masque les
/// actions pour l'ergonomie (plan §4 WS-2 / R-07, R-08).
/// </summary>
public static class PayrollSourcedEntryGuard
{
    /// <summary>Types sources désignant une écriture générée par le module Paie (non extournable manuellement).</summary>
    public static readonly HashSet<string> PayrollSourceTypes = new(StringComparer.Ordinal)
    {
        "PayrollRun",
        "PayrollPayment",
        "CnssContributionPayment",
        "EmployeeAdvance",
        "EmployeeLoan"
    };

    /// <summary>Vrai si le type source désigne une écriture paie (null/empty → faux).</summary>
    public static bool IsSystemSource(string? sourceEntityType) =>
        !string.IsNullOrEmpty(sourceEntityType) && PayrollSourceTypes.Contains(sourceEntityType);
}
