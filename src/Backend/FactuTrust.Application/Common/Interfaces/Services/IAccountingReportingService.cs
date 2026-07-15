using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAccountingReportingService
{
    Task<Result<IReadOnlyList<ChartOfAccountDto>>> GetChartOfAccountsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<JournalEntryDto>>> GetJournalEntriesAsync(
        string? journalCode,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<LedgerRowDto>>> GetLedgerAsync(
        string accountNumber,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BalanceRowDto>>> GetBalanceAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
    Task<Result<AccountingDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AccountingPeriodDto>>> GetPeriodsAsync(CancellationToken cancellationToken = default);
    Task<Result<BalanceSheetDto>> GetBalanceSheetAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task<Result<IncomeStatementDto>> GetIncomeStatementAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>Liasse NCT (bilan + résultat structurés + flux + variation capitaux + notes), sur écritures validées, avec comparatif N-1.</summary>
    Task<Result<NctFinancialStatementsDto>> GetNctStatementsAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Balance auxiliaire : une ligne par tiers (client ou fournisseur) — ouverture, mouvements,
    /// clôture — à partir des lignes d'écriture portant un ThirdPartyId. Brouillons inclus selon
    /// la politique de consultation.
    /// </summary>
    Task<Result<IReadOnlyList<AuxiliaryBalanceRowDto>>> GetAuxiliaryBalanceAsync(
        ThirdPartyKind kind, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Grand livre d'un tiers : solde d'ouverture + mouvements avec solde progressif.</summary>
    Task<Result<ThirdPartyLedgerDto>> GetThirdPartyLedgerAsync(
        Guid thirdPartyId, ThirdPartyKind kind, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Recherche multicritère de lignes d'écriture (compte, journal, période, montant, libellé, lettrage, statut, réf. pièce).</summary>
    Task<Result<IReadOnlyList<JournalSearchRowDto>>> SearchJournalEntriesAsync(
        string? accountNumber, string? journalCode, DateTime? from, DateTime? to,
        decimal? minAmount, decimal? maxAmount, string? label, string? letteringCode, int? status, int take,
        string? pieceRef = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// État budgétaire : budget (Initial/Révisé) vs réalisé par poste sur un exercice, avec détail
    /// mensuel, cumuls jusqu'au mois demandé, écart et % de consommation. Le réalisé affecte chaque
    /// compte au poste dont le préfixe correspondant est le plus long (compté une seule fois) ; les
    /// comptes des classes 6/7 non couverts alimentent des lignes « Hors postes ».
    /// </summary>
    Task<Result<BudgetReportDto>> GetBudgetReportAsync(
        int fiscalYear, int? throughMonth, CancellationToken cancellationToken = default);
}
