using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IFixedAssetRepository
{
    Task<FixedAsset?> GetByIdAsync(Guid id, bool includeSchedule = false, bool includeEvents = false, CancellationToken cancellationToken = default);
    Task<FixedAsset?> GetByInventoryNumberAsync(string inventoryNumber, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FixedAsset> Items, int TotalCount)> SearchAsync(
        int page,
        int pageSize,
        FixedAssetStatus? status,
        Guid? categoryId,
        int? fiscalYear,
        string? search,
        int fiscalYearStartMonth = 1,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FixedAssetAmortizationTableRowDto> Items, int TotalCount)> SearchCurrentYearAmortizationTableAsync(
        int page,
        int pageSize,
        int fiscalYear,
        FixedAssetStatus? status,
        Guid? categoryId,
        string? search,
        CancellationToken cancellationToken = default);
    Task<AmortizationReportResponse> GetAmortizationReportAsync(
        int fiscalYear,
        AmortizationReportGroupingMode groupingMode,
        FixedAssetStatus? status,
        Guid? categoryId,
        string? search,
        string companyName,
        CancellationToken cancellationToken = default);
    Task<int> CountByYearPrefixAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prochaine séquence disponible pour l'année (plan T5, B2) : <c>MAX(suffixe numérique) + 1</c>
    /// sur les numéros <c>IMMO-{year}-%</c> — contrairement à un COUNT+1, correct en présence de trous
    /// (actifs supprimés/numéros sautés).
    /// </summary>
    Task<int> GetNextInventorySequenceAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée un actif avec un numéro d'inventaire généré, avec retry ciblé (plan T5, B2) : à chaque
    /// tentative, la séquence est recalculée et l'entité entièrement reconstruite via
    /// <paramref name="factory"/> (jamais de mutation d'une entité déjà trackée par un contexte
    /// invalidé) dans un nouveau contexte. Retry uniquement sur violation de
    /// <c>IX_FixedAssets_InventoryNumber</c> (SQL 2601/2627) ; toute autre erreur est propagée
    /// immédiatement, sans retry.
    /// </summary>
    Task<Result<FixedAsset>> AddWithGeneratedInventoryNumberAsync(
        Func<string, Result<FixedAsset>> factory,
        int year,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FixedAsset>> GetBySupplierInvoiceIdAsync(Guid supplierInvoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FixedAsset>> GetActiveForDepreciationRunAsync(int fiscalYear, int fiscalYearStartMonth = 1, CancellationToken cancellationToken = default);
    Task<FixedAsset> AddAsync(FixedAsset entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(FixedAsset entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mise en service atomique : statut, événement InService et remplacement optionnel du tableau dans une seule transaction.
    /// </summary>
    Task<Result<FixedAsset>> PutInServiceInTransactionAsync(
        Guid id,
        DateTime inServiceDate,
        string creditAccountNumber,
        IReadOnlyList<DepreciationScheduleLine>? scheduleLinesToReplace,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task ReplaceScheduleLinesAsync(Guid fixedAssetId, IReadOnlyList<DepreciationScheduleLine> lines, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remplace en place les lignes d'échéancier non comptabilisées (flux de cession — T4, B1) :
    /// met à jour la ligne de l'année de cession avec la dotation prorata (via
    /// <c>UpdateAmounts</c>, <c>Id</c> et lien d'audit conservés) et supprime les lignes non
    /// postées d'exercices postérieurs. Les lignes <c>IsPosted</c> ne sont **jamais** touchées.
    /// Toutes les méthodes appelées utilisent <c>CreateContext()</c> (enrôlement transaction ambiante).
    /// </summary>
    Task ReplaceUnpostedScheduleLinesAsync(Guid fixedAssetId, IReadOnlyList<DepreciationScheduleLine> targetLines, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepreciationScheduleLine>> GetUnpostedScheduleLinesForYearAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task SaveScheduleLineAsync(DepreciationScheduleLine line, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nombre de lignes déjà comptabilisées pour l'exercice (plan T7, B4) — sert à distinguer,
    /// lors d'un nouveau lancement de la comptabilisation des dotations, un exercice « déjà
    /// entièrement comptabilisé » d'un exercice sans aucune dotation à comptabiliser.
    /// </summary>
    Task<int> GetPostedScheduleLineCountForYearAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lignes d'échéancier d'un actif avec l'état d'extourne de leur écriture comptable
    /// (T13, C6) : jointure LINQ explicite
    /// <c>DepreciationScheduleLines LEFT JOIN JournalEntries ON JournalEntryId = JournalEntries.Id</c>,
    /// projetée en <c>(Line, IsReversed)</c>. **Conservateur** : <c>JournalEntryId</c> nul ou
    /// écriture introuvable → <c>IsReversed = false</c> (une ligne postée sans extourne prouvée
    /// reste bloquante pour la régénération).
    /// </summary>
    Task<IReadOnlyList<(DepreciationScheduleLine Line, bool IsReversed)>> GetScheduleLinesWithReversalStateAsync(
        Guid fixedAssetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste uniquement les cumuls d'amortissement de l'actif (amortissement cumulé, VNC,
    /// statut, audit, version) **sans retoucher les lignes d'échéancier** (T13, C6) — utilisé
    /// après recalcul du cumul (<c>FixedAsset.RecalculateDepreciationTotals</c>) lors d'une
    /// régénération post-extourne. S'enrôle dans la transaction ambiante via <c>CreateContext()</c>.
    /// </summary>
    Task UpdateDepreciationTotalsAsync(FixedAsset asset, CancellationToken cancellationToken = default);
}
