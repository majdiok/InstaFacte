using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;

using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Superpose le réglage d'imputation du dossier à la configuration globale. Chaque champ non
/// renseigné côté tenant retombe sur <see cref="AccountingSettings"/> : un dossier sans ligne de
/// paramétrage produit donc exactement l'instantané global, et son comportement est inchangé.
/// </summary>
public sealed class PayrollAccountingProfileResolver : IPayrollAccountingProfileResolver
{
    private readonly IPayrollAccountingSettingsRepository _repository;
    private readonly AccountingSettings _settings;

    public PayrollAccountingProfileResolver(
        IPayrollAccountingSettingsRepository repository,
        IOptions<AccountingSettings> settings)
    {
        _repository = repository;
        _settings = settings.Value;
    }

    public async Task<PayrollAccountingProfileSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var fallback = _settings.ToPayrollProfileSnapshot();
        var tenant = await _repository.GetForTenantAsync(cancellationToken);
        if (tenant is null)
            return fallback;

        return fallback with
        {
            Profile = tenant.AccountProfile,
            EffectiveDate = tenant.AccountProfileEffectiveDate,
            InKindOffsetAccount = string.IsNullOrWhiteSpace(tenant.InKindOffsetAccount)
                ? fallback.InKindOffsetAccount
                : tenant.InKindOffsetAccount,
            DisbursementEntriesEnabled = tenant.DisbursementEntriesEnabled,
            DetailedSalarySplitEnabled = tenant.DetailedSalarySplitEnabled,
            EmployeeAuxiliaryEnabled = tenant.EmployeeAuxiliaryEnabled,
            IsTenantOverride = true
        };
    }
}
