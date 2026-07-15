using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Comptabilité budgétaire : postes budgétaires, statut d'exercice et lignes mensuelles
/// (versions Initial/Révisé). Les opérations multi-lignes sont atomiques (un seul SaveChanges).
/// </summary>
public interface IBudgetRepository
{
    Task<IReadOnlyList<BudgetPost>> GetPostsAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<BudgetPost?> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BudgetPost?> GetPostByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddPostAsync(BudgetPost post, CancellationToken cancellationToken = default);
    Task UpdatePostAsync(BudgetPost post, CancellationToken cancellationToken = default);

    Task<BudgetYear?> GetYearAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BudgetLine>> GetLinesAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remplace les valeurs de la version donnée pour les postes fournis (montant zéro = suppression).
    /// Crée le <see cref="BudgetYear"/> en brouillon s'il n'existe pas. Atomique.
    /// </summary>
    Task<Result> UpsertYearLinesAsync(int fiscalYear, BudgetVersion version,
        IReadOnlyList<(Guid PostId, int Month, decimal Amount)> lines, string user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide (fige) le budget initial de l'exercice : statut → Validated, copie des lignes
    /// Initial vers Révisé (point de départ des révisions). Atomique ; refuse une revalidation.
    /// </summary>
    Task<Result> ValidateInitialAsync(int fiscalYear, string user, CancellationToken cancellationToken = default);
}
