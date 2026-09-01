namespace FactuTrust.Application.DTOs;

/// <summary>
/// Réglage d'imputation comptable de la paie effectivement appliqué au dossier.
/// </summary>
public sealed record PayrollAccountingSettingsDto
{
    /// <summary><c>Legacy</c> ou <c>Sce2026</c>.</summary>
    public string AccountProfile { get; init; } = null!;

    /// <summary>Premier jour du mois à partir duquel le profil s'applique ; <c>null</c> = tous les cycles.</summary>
    public DateTime? AccountProfileEffectiveDate { get; init; }

    public string InKindOffsetAccount { get; init; } = null!;
    public bool DisbursementEntriesEnabled { get; init; }
    public bool DetailedSalarySplitEnabled { get; init; }
    public bool EmployeeAuxiliaryEnabled { get; init; }

    /// <summary>Faux = aucun réglage propre au dossier, les valeurs proviennent de la configuration globale.</summary>
    public bool IsTenantOverride { get; init; }

    /// <summary>
    /// Période du dernier cycle validé ou clôturé (<c>yyyy-MM</c>), ou <c>null</c> s'il n'y en a
    /// aucun. La date de bascule doit lui être postérieure : l'écran s'en sert pour proposer une
    /// date valide et expliquer le refus le cas échéant.
    /// </summary>
    public string? LastSettledPeriod { get; init; }

    /// <summary>Première date de bascule acceptable (1er du mois suivant le dernier cycle arrêté).</summary>
    public DateTime? EarliestEffectiveDate { get; init; }
}

/// <summary>Corps de <c>PUT api/payroll/settings/accounting</c>.</summary>
public sealed record UpdatePayrollAccountingSettingsRequest
{
    public string AccountProfile { get; init; } = null!;
    public DateTime? AccountProfileEffectiveDate { get; init; }
    public string? InKindOffsetAccount { get; init; }
    public bool DisbursementEntriesEnabled { get; init; }
    public bool DetailedSalarySplitEnabled { get; init; }
    public bool EmployeeAuxiliaryEnabled { get; init; } = true;
}
