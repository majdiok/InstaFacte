using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Application.Configuration;

/// <summary>
/// Configuration d'imputation comptable de la paie effectivement applicable à un dossier :
/// réglage persisté du tenant s'il existe, sinon repli sur la configuration globale
/// (<see cref="AccountingSettings"/>).
/// </summary>
/// <remarks>
/// Source unique de la résolution du profil et de la carte de comptes. La génération réelle
/// (<c>AccountingService</c>), la simulation du journal de paie et l'écran de paramètres lisent
/// tous cet instantané : une seule implémentation, donc aucune divergence possible entre ce que
/// l'écran annonce et ce que l'écriture produit.
/// </remarks>
public sealed record PayrollAccountingProfileSnapshot
{
    public required PayrollAccountProfile Profile { get; init; }

    /// <summary>Premier jour du mois à partir duquel <see cref="Profile"/> s'applique ; <c>null</c> = tous les cycles.</summary>
    public DateTime? EffectiveDate { get; init; }

    public required string InKindOffsetAccount { get; init; }
    public required string LoansAccount { get; init; }
    public required string GarnishmentsAccount { get; init; }
    public required string MutuelleEmployeeAccount { get; init; }
    public required string MealVoucherEmployeeAccount { get; init; }

    public bool DisbursementEntriesEnabled { get; init; }
    public bool DetailedSalarySplitEnabled { get; init; }

    /// <summary>Ventile le crédit 425 par salarié (comptes auxiliaires) plutôt qu'en une ligne agrégée.</summary>
    public bool EmployeeAuxiliaryEnabled { get; init; } = true;

    /// <summary>Vrai si le dossier porte un réglage propre (faux = valeurs de la configuration globale).</summary>
    public bool IsTenantOverride { get; init; }

    /// <summary>
    /// Profil applicable au cycle <paramref name="year"/>/<paramref name="month"/>. Avant la date de
    /// bascule → <see cref="PayrollAccountProfile.Legacy"/>, de sorte qu'une réouverture suivie
    /// d'une revalidation régénère exactement les mêmes comptes qu'à l'origine ; à partir de la date
    /// → <see cref="Profile"/>. Sans date de bascule, <see cref="Profile"/> s'applique à tous les cycles.
    /// </summary>
    public PayrollAccountProfile ResolveForPeriod(int year, int month)
    {
        if (EffectiveDate is not { } effective)
            return Profile;

        // Dernier jour du mois du cycle : un cycle est « antérieur » tant qu'il se termine avant la bascule.
        var periodEnd = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        return periodEnd < effective ? PayrollAccountProfile.Legacy : Profile;
    }

    /// <summary>
    /// Carte de comptes de l'OD de paie pour le profil résolu. Sous <c>Legacy</c>, la compensation
    /// d'avantage en nature et les retenues non typées restent sur le compte historique 421 :
    /// l'imputation d'un cycle antérieur à la bascule ne doit jamais changer.
    /// </summary>
    public PayrollJournalEntryAccountMap BuildAccountMap(PayrollAccountProfile profile) => new()
    {
        LoansAccount = LoansAccount,
        GarnishmentsAccount = GarnishmentsAccount,
        MutuelleEmployeeAccount = MutuelleEmployeeAccount,
        MealVoucherEmployeeAccount = MealVoucherEmployeeAccount,
        InKindBenefitOffsetAccount = profile == PayrollAccountProfile.Sce2026
            ? InKindOffsetAccount
            : PayrollJournalEntryBuilder.AdvancesAccount,
        DefaultOtherAccount = profile == PayrollAccountProfile.Sce2026
            ? PayrollJournalEntryBuilder.OtherDeductionsPayableAccount
            : PayrollJournalEntryBuilder.AdvancesAccount
    };
}
