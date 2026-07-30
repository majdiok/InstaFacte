using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Encours d'un client : ce qu'il doit, et ce qu'il s'apprête à devoir.
/// </summary>
public sealed class ClientOutstandingDto
{
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;

    /// <summary>Factures émises et non soldées : TTC restant après encaissements.</summary>
    public decimal UnpaidInvoicesAmount { get; init; }

    /// <summary>
    /// Commandes confirmées pas encore facturées. Elles ne sont pas une créance, mais un
    /// engagement : les ignorer ferait découvrir le dépassement à la facturation, trop tard.
    /// </summary>
    public decimal ConfirmedOrdersAmount { get; init; }

    /// <summary>Encours total retenu pour l'alerte.</summary>
    public decimal TotalOutstanding { get; init; }

    /// <summary>Plafond du client, ou <c>null</c> s'il n'en a pas.</summary>
    public decimal? CreditLimit { get; init; }

    /// <summary>Marge restante sous le plafond. Négative en dépassement, <c>null</c> sans plafond.</summary>
    public decimal? AvailableCredit { get; init; }

    /// <summary>
    /// Vrai si l'encours dépasse le plafond. <b>Purement informatif</b> : aucun traitement ne
    /// doit refuser une opération sur cette base.
    /// </summary>
    public bool IsOverLimit { get; init; }

    /// <summary>Nombre de factures non soldées, pour situer l'encours.</summary>
    public int UnpaidInvoiceCount { get; init; }

    /// <summary>Montant échu depuis plus de 30 jours — le signal qui compte vraiment.</summary>
    public decimal OverdueAmount { get; init; }

    public string Currency { get; init; } = "TND";
}

/// <summary>
/// Calcule l'encours d'un client : factures non soldées + commandes confirmées non facturées.
///
/// ⚠️ <b>Alerte seule.</b> Décision produit actée : aucun appelant ne doit refuser un devis, une
/// commande ou une facture parce que le plafond est dépassé. Le service informe ; le commercial
/// décide. Un plafond mal tenu bloquerait des ventes légitimes.
/// </summary>
public interface IClientOutstandingService
{
    Task<Result<ClientOutstandingDto>> GetOutstandingAsync(
        Guid clientId, CancellationToken cancellationToken = default);
}
