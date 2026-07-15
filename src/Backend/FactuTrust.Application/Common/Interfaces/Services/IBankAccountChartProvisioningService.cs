using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Provisionne et valide la liaison d'un compte bancaire (trésorerie) vers un compte 532x du plan comptable.
/// </summary>
public interface IBankAccountChartProvisioningService
{
    /// <summary>
    /// Lie un compte existant ou crée un sous-compte auxiliaire 532x pour le compte bancaire.
    /// </summary>
    Task<Result<string?>> ResolveChartAccountNumberAsync(
        BankAccount bankAccount,
        string? requestedChartAccountNumber,
        bool autoCreate,
        CancellationToken cancellationToken = default);

    Task<Result> ValidateChartAccountNumberAsync(string chartAccountNumber, CancellationToken cancellationToken = default);
}
