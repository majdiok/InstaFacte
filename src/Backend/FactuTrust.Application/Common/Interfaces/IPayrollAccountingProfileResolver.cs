using FactuTrust.Application.Configuration;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Résout la configuration d'imputation comptable de la paie du dossier courant : réglage persisté
/// du tenant s'il existe, sinon repli sur <see cref="AccountingSettings"/> (configuration globale).
/// </summary>
public interface IPayrollAccountingProfileResolver
{
    Task<PayrollAccountingProfileSnapshot> GetAsync(CancellationToken cancellationToken = default);
}
