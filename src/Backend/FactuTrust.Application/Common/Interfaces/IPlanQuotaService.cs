using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Lot C1 — Service applicatif d'enforcement des quotas du plan tarifaire
/// (factures/mois, etc.). Centralise les vérifications avant écriture et
/// l'incrémentation post-écriture du compteur <c>Subscription.InvoicesThisMonth</c>.
///
/// <para>
/// Pourquoi cette interface ? Le handler <c>CreateInvoiceCommandHandler</c> vit dans
/// le tenant context (DbContext tenant), tandis que les <c>Subscriptions</c> vivent
/// dans la Master DB. Ce service encapsule l'accès Master + la logique de reset
/// mensuel pour ne pas polluer le handler factures.
/// </para>
/// </summary>
public interface IPlanQuotaService
{
    /// <summary>
    /// Vérifie qu'un tenant peut créer une nouvelle facture (limite mensuelle non atteinte,
    /// abonnement actif). Réinitialise au passage le compteur mensuel si on a changé de mois.
    /// </summary>
    /// <returns>
    /// <see cref="Result.Success()"/> si autorisé, sinon <see cref="Error.Validation"/> avec
    /// un message FR explicite.
    /// </returns>
    Task<Result> EnsureCanCreateInvoiceAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// À appeler APRÈS la création réussie d'une facture : incrémente
    /// <c>Subscription.InvoicesThisMonth</c>. Aucune exception levée si la Subscription
    /// n'est pas trouvée (best-effort — la facture est déjà créée).
    /// </summary>
    Task OnInvoiceCreatedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
