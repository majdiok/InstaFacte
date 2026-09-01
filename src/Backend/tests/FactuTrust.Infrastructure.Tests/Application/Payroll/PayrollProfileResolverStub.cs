using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Résolveur de profil d'imputation adossé à une <see cref="AccountingSettings"/> en mémoire —
/// équivalent d'un dossier sans réglage propre, qui hérite donc de la configuration globale.
/// </summary>
internal sealed class PayrollProfileResolverStub : IPayrollAccountingProfileResolver
{
    private readonly PayrollAccountingProfileSnapshot _snapshot;

    public PayrollProfileResolverStub(AccountingSettings settings)
        => _snapshot = settings.ToPayrollProfileSnapshot();

    public PayrollProfileResolverStub(PayrollAccountingProfileSnapshot snapshot)
        => _snapshot = snapshot;

    public Task<PayrollAccountingProfileSnapshot> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_snapshot);
}
