using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Ventilation des retenues paie vers les comptes SCE.
/// </summary>
public sealed class PayrollJournalEntryAccountMap
{
    public string AdvancesAccount { get; init; } = PayrollJournalEntryBuilder.AdvancesAccount;
    public string LoansAccount { get; init; } = "421.1";
    public string GarnishmentsAccount { get; init; } = "427";
    public string MutuelleEmployeeAccount { get; init; } = "428.1";
    public string MealVoucherEmployeeAccount { get; init; } = "428.2";
    /// <summary>
    /// Compte SCE de compensation de l'avantage en nature (retenue salarié). Défaut doctrinal 4386
    /// « Autres charges à payer » (plan §4.1.1 m / Q3). Le profil Legacy force le compte historique 421.
    /// </summary>
    public string InKindBenefitOffsetAccount { get; init; } = PayrollJournalEntryBuilder.InKindBenefitOffsetPayableAccount;
    /// <summary>
    /// Compte SCE de la dette patronale des fonds sociaux / mutuelle (part employeur). Défaut 4538
    /// « Organismes sociaux - charges à payer » ; surchargeable par le compte du régime
    /// (<c>SocialFundScheme.EmployerAccountSce</c>) figé sur la ligne de bulletin.
    /// </summary>
    public string SocialFundEmployerPayableAccount { get; init; } = PayrollJournalEntryBuilder.SocialFundEmployerPayableAccount;
    public string DefaultOtherAccount { get; init; } = PayrollJournalEntryBuilder.AdvancesAccount;

    public string ResolveCreditAccount(DeductionKind kind, string? schemeAccountSce = null) => kind switch
    {
        DeductionKind.Advance => AdvancesAccount,
        DeductionKind.Loan => LoansAccount,
        DeductionKind.Garnishment => GarnishmentsAccount,
        DeductionKind.Alimony => GarnishmentsAccount,
        // Compte du régime figé sur la ligne (R-14) ; repli sur le compte paramétré puis 428.1.
        DeductionKind.MutuelleEmployee => schemeAccountSce ?? MutuelleEmployeeAccount,
        DeductionKind.MealVoucherEmployeeShare => MealVoucherEmployeeAccount,
        // Compensation AN : compte du régime si figé, sinon le compte de compensation configuré.
        DeductionKind.InKindBenefitOffset => schemeAccountSce ?? InKindBenefitOffsetAccount,
        DeductionKind.Other => DefaultOtherAccount,
        _ => DefaultOtherAccount
    };
}
