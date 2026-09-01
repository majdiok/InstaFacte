using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record PayrollFeatureFlagsDto
{
    public bool StatutorySickLeaveEnabled { get; init; }
    public bool StatutoryMaternityLeaveEnabled { get; init; }
    public bool StatutoryPaternityLeaveEnabled { get; init; }
    public bool TerminationIndemnityEnabled { get; init; }
    public bool HrDocumentsEnabled { get; init; }
    public bool AnnualBonusesEnabled { get; init; }
    public bool PublicHolidaysEnabled { get; init; }
    public bool CivpEnhancementsEnabled { get; init; }
    public bool CnssCeilingsEnabled { get; init; }
    public bool LegalPresetsHistoryEnabled { get; init; }

    /// <summary>
    /// Configuration SCE de comptabilisation de la paie effectivement appliquée au dossier :
    /// réglage propre du dossier s'il existe, sinon configuration globale
    /// (<see cref="AccountingSettings"/>). Le réglage s'édite via
    /// <c>PUT api/payroll/settings/accounting</c>.
    /// </summary>
    public string? PayrollAccountProfile { get; init; }
    public DateTime? PayrollAccountProfileEffectiveDate { get; init; }
    public string? PayrollInKindOffsetAccount { get; init; }
    public bool PayrollDisbursementEntriesEnabled { get; init; }
    public bool PayrollDetailedSalarySplitEnabled { get; init; }
    public bool PayrollStrictSettlementEnabled { get; init; }

    /// <summary>Vrai si le dossier porte un réglage propre (faux = valeurs globales héritées).</summary>
    public bool PayrollAccountProfileIsTenantOverride { get; init; }
}

public sealed record GetPayrollFeatureFlagsQuery : IRequest<PayrollFeatureFlagsDto>;

public sealed class GetPayrollFeatureFlagsQueryHandler : IRequestHandler<GetPayrollFeatureFlagsQuery, PayrollFeatureFlagsDto>
{
    private readonly AccountingSettings _settings;
    private readonly IPayrollAccountingProfileResolver _profileResolver;

    public GetPayrollFeatureFlagsQueryHandler(
        IOptions<AccountingSettings> settings,
        IPayrollAccountingProfileResolver profileResolver)
    {
        _settings = settings.Value;
        _profileResolver = profileResolver;
    }

    public async Task<PayrollFeatureFlagsDto> Handle(GetPayrollFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var payrollProfile = await _profileResolver.GetAsync(cancellationToken);

        return new PayrollFeatureFlagsDto
        {
            StatutorySickLeaveEnabled = _settings.PayrollStatutorySickLeaveEnabled,
            StatutoryMaternityLeaveEnabled = _settings.PayrollStatutoryMaternityLeaveEnabled,
            StatutoryPaternityLeaveEnabled = _settings.PayrollStatutoryPaternityLeaveEnabled,
            TerminationIndemnityEnabled = _settings.PayrollTerminationIndemnityEnabled,
            HrDocumentsEnabled = _settings.PayrollHrDocumentsEnabled,
            AnnualBonusesEnabled = _settings.PayrollAnnualBonusesEnabled,
            PublicHolidaysEnabled = _settings.PayrollPublicHolidaysEnabled,
            CivpEnhancementsEnabled = _settings.PayrollCivpEnhancementsEnabled,
            CnssCeilingsEnabled = _settings.PayrollCnssCeilingsEnabled,
            LegalPresetsHistoryEnabled = _settings.PayrollLegalPresetsHistoryEnabled,
            PayrollAccountProfile = payrollProfile.Profile.ToString(),
            PayrollAccountProfileEffectiveDate = payrollProfile.EffectiveDate,
            PayrollInKindOffsetAccount = payrollProfile.InKindOffsetAccount,
            PayrollDisbursementEntriesEnabled = payrollProfile.DisbursementEntriesEnabled,
            PayrollDetailedSalarySplitEnabled = payrollProfile.DetailedSalarySplitEnabled,
            PayrollStrictSettlementEnabled = _settings.PayrollStrictSettlementEnabled,
            PayrollAccountProfileIsTenantOverride = payrollProfile.IsTenantOverride
        };
    }
}
