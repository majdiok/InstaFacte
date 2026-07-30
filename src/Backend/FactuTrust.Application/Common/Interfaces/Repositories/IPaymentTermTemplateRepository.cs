using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Pricing;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Repository des conditions de règlement structurées.</summary>
public interface IPaymentTermTemplateRepository : IRepository<PaymentTermTemplate>
{
    /// <summary>Conditions actives, proposées à la saisie d'un document.</summary>
    Task<IReadOnlyList<PaymentTermTemplate>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Condition marquée par défaut, ou <c>null</c> s'il n'y en a pas.</summary>
    Task<PaymentTermTemplate?> GetDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retire le drapeau « par défaut » de toutes les autres conditions. L'index unique filtré
    /// refuserait un second défaut : on nettoie donc AVANT d'en poser un nouveau.
    /// </summary>
    Task ClearDefaultAsync(Guid exceptId, CancellationToken cancellationToken = default);
}
