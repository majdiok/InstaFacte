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
    public string InKindBenefitOffsetAccount { get; init; } = PayrollJournalEntryBuilder.AdvancesAccount;
    public string DefaultOtherAccount { get; init; } = PayrollJournalEntryBuilder.AdvancesAccount;

    public string ResolveCreditAccount(DeductionKind kind, string? schemeAccountSce = null) => kind switch
    {
        DeductionKind.Advance => AdvancesAccount,
        DeductionKind.Loan => LoansAccount,
        DeductionKind.Garnishment => GarnishmentsAccount,
        DeductionKind.Alimony => GarnishmentsAccount,
        DeductionKind.MutuelleEmployee => schemeAccountSce ?? MutuelleEmployeeAccount,
        DeductionKind.MealVoucherEmployeeShare => MealVoucherEmployeeAccount,
        DeductionKind.InKindBenefitOffset => InKindBenefitOffsetAccount,
        DeductionKind.Other => DefaultOtherAccount,
        _ => DefaultOtherAccount
    };
}
