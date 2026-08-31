using FactuTrust.Application.Features.Payroll.AnnualBonuses;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Application.Features.Payroll.Declarations.CnssRemittance;
using FactuTrust.Application.Features.Payroll.Payments;
using FactuTrust.Application.Features.Payroll.PublicHolidays;
using FactuTrust.Application.Features.Payroll.Regularization;
using FactuTrust.Application.Features.Payroll.Reports;
using FactuTrust.Application.Features.Payroll.SocialFunds;

namespace FactuTrust.Application.Authorization;

/// <summary>MediatR requests that require delegated firm context when a cabinet assignment is active.</summary>
public static class PayrollFirmExclusiveRequests
{
    private static readonly HashSet<Type> Types = new()
    {
        typeof(CreatePayrollRunCommand),
        typeof(CalculatePayrollRunCommand),
        typeof(ValidatePayrollRunCommand),
        typeof(ReopenPayrollRunCommand),
        typeof(ClosePayrollRunCommand),
        typeof(UpdatePayrollParametersCommand),
        typeof(UpdatePayrollGarnishmentBracketsCommand),
        typeof(GenerateIrppRegularizationsCommand),
        typeof(UpsertIrppRegularizationCommand),
        typeof(DeleteIrppRegularizationCommand),
        typeof(CreatePublicHolidayCommand),
        typeof(UpdatePublicHolidayCommand),
        typeof(DeletePublicHolidayCommand),
        typeof(SeedPublicHolidaysCommand),
        typeof(CreateAnnualBonusRuleCommand),
        typeof(UpdateAnnualBonusRuleCommand),
        typeof(DeleteAnnualBonusRuleCommand),
        typeof(CreateSocialFundSchemeCommand),
        typeof(UpdateSocialFundSchemeCommand),
        typeof(RecordPayrollRunPaymentCommand),
        typeof(RecordPayslipPaymentCommand),
        typeof(CancelPayrollPaymentCommand),
        typeof(CancelAllPayrollRunPaymentsCommand),
        typeof(RecordCnssContributionPaymentCommand),
        typeof(CancelCnssContributionPaymentCommand),
        // ── WS-5 : outils firm-only de remédiation historique & diagnostic SCE ──
        typeof(PayrollComplianceDiagnosticQuery),
        typeof(GeneratePayrollReclassificationCommand),
        typeof(UnsettleAdvanceCommand),
        typeof(UnsettleLoanInstallmentCommand),
        typeof(GetPayrollExposureReportQuery),
        typeof(ExportPayrollExposureReportQuery)
    };

    public static bool IsFirmExclusiveRequest(Type requestType) =>
        Types.Contains(requestType);

    public static IReadOnlyCollection<Type> All => Types;
}
