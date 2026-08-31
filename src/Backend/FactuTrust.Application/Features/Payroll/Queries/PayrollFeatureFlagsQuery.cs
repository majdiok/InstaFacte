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

    /// <summary>Configuration SCE de comptabilisation de la paie (plan §4 WS-1 / §5.3).
    /// Lecture seule : <see cref="AccountingSettings"/> est une configuration globale (IOptions),
    /// non persistée par tenant — la modification est réservée à l'administrateur (appsettings).</summary>
    public string? PayrollAccountProfile { get; init; }
    public DateTime? PayrollAccountProfileEffectiveDate { get; init; }
    public string? PayrollInKindOffsetAccount { get; init; }
    public bool PayrollDisbursementEntriesEnabled { get; init; }
    public bool PayrollDetailedSalarySplitEnabled { get; init; }
    public bool PayrollStrictSettlementEnabled { get; init; }
}

public sealed record GetPayrollFeatureFlagsQuery : IRequest<PayrollFeatureFlagsDto>;

public sealed class GetPayrollFeatureFlagsQueryHandler : IRequestHandler<GetPayrollFeatureFlagsQuery, PayrollFeatureFlagsDto>
{
    private readonly AccountingSettings _settings;

    public GetPayrollFeatureFlagsQueryHandler(IOptions<AccountingSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<PayrollFeatureFlagsDto> Handle(GetPayrollFeatureFlagsQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new PayrollFeatureFlagsDto
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
            PayrollAccountProfile = _settings.PayrollAccountProfile.ToString(),
            PayrollAccountProfileEffectiveDate = _settings.PayrollAccountProfileEffectiveDate,
            PayrollInKindOffsetAccount = _settings.PayrollInKindOffsetAccount,
            PayrollDisbursementEntriesEnabled = _settings.PayrollDisbursementEntriesEnabled,
            PayrollDetailedSalarySplitEnabled = _settings.PayrollDetailedSalarySplitEnabled,
            PayrollStrictSettlementEnabled = _settings.PayrollStrictSettlementEnabled
        });
}
