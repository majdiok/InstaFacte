using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository de l'agrégat <see cref="SalesOrder"/>.
///
/// Calqué sur <see cref="IPurchaseOrderRepository"/>, avec en plus
/// <see cref="GetBacklogAsync"/> : le carnet de commandes est la raison d'être du module,
/// et n'a pas d'équivalent côté achats.
/// </summary>
public interface ISalesOrderRepository : IRepository<SalesOrder>
{
    /// <summary>Charge une commande avec ses lignes, le client et les produits.</summary>
    Task<SalesOrder?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Recherche paginée et filtrée.</summary>
    Task<(IReadOnlyList<SalesOrder> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        SalesOrderStatus? status,
        Guid? clientId,
        DateTime? fromDate,
        DateTime? toDate,
        bool openOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Totaux agrégés sur l'ENSEMBLE du jeu filtré (mêmes filtres que
    /// <see cref="SearchAsync"/>, sans pagination). Alimente la zone de totaux de la liste.
    /// </summary>
    Task<SalesOrderListSummaryDto> GetSummaryAsync(
        string? searchTerm,
        SalesOrderStatus? status,
        Guid? clientId,
        DateTime? fromDate,
        DateTime? toDate,
        bool openOnly,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Carnet de commandes : lignes des commandes ouvertes restant à livrer, avec leur
    /// valeur HT au prorata. C'est l'indicateur que l'absence de commande client rendait
    /// impossible à produire.
    /// </summary>
    Task<IReadOnlyList<SalesOrderBacklogRowDto>> GetBacklogAsync(
        Guid? clientId,
        DateTime? dueBefore,
        CancellationToken cancellationToken = default);

    /// <summary>Commandes ouvertes d'un client — alimente le calcul d'encours (lot 6).</summary>
    Task<IReadOnlyList<SalesOrder>> GetOpenOrdersByClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default);
}
