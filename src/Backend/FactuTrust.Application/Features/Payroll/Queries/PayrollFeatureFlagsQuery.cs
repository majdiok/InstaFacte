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
            LegalPresetsHistoryEnabled = _settings.PayrollLegalPresetsHistoryEnabled
        });
}
