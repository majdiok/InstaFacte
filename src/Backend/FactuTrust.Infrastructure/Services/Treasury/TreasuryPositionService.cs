using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Treasury;

/// <summary>
/// Calcule la position de trésorerie à une date à partir de la balance comptable, en ne retenant
/// que les comptes financiers réels (banques et caisses) de la classe 5.
/// </summary>
/// <remarks>
/// Reprend le patron d'ancrage déjà employé par <c>BankReconciliationService</c> pour rapprocher un
/// relevé : balance de l'exercice jusqu'à la date, puis solde débiteur net du compte. La différence
/// est qu'on consolide ici tous les comptes de trésorerie au lieu d'un seul.
/// </remarks>
public sealed class TreasuryPositionService : ITreasuryPositionService
{
    private readonly IAccountingReportingService _reporting;
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ILogger<TreasuryPositionService> _logger;

    public TreasuryPositionService(
        IAccountingReportingService reporting,
        ITenantDbContextFactory contextFactory,
        ILogger<TreasuryPositionService> logger)
    {
        _reporting = reporting;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<CashPositionDto> GetCashPositionAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default)
    {
        var date = asOf.Date;

        // La balance se lit depuis le début de l'exercice civil : c'est la fenêtre qui porte
        // l'à-nouveau, donc le seul point d'ancrage correct pour un solde de clôture à une date.
        var from = new DateTime(date.Year, 1, 1);

        var balance = await _reporting.GetBalanceAsync(from, date, cancellationToken);
        if (balance.IsFailure)
        {
            // Une projection sans solde d'ouverture est trompeuse, mais un échec de balance ne doit
            // pas faire tomber l'écran : on retourne une position vide, l'appelant signale l'écart.
            _logger.LogWarning(
                "Position de trésorerie au {AsOf} : balance indisponible ({Error}). Position vide retournée.",
                date,
                balance.Error);

            return new CashPositionDto { AsOf = date };
        }

        var treasuryRows = balance.Value
            .Where(row => TreasuryAccountRoots.IsTreasuryAccount(row.AccountNumber))
            .ToList();

        if (treasuryRows.Count == 0)
            return new CashPositionDto { AsOf = date };

        var bankAccountsByChart = await LoadBankAccountsByChartAsync(cancellationToken);

        var accounts = treasuryRows
            .Select(row =>
            {
                var accountNumber = row.AccountNumber.Trim();
                return new CashPositionAccountDto
                {
                    AccountNumber = accountNumber,
                    Label = row.Label,
                    Balance = TreasuryAccountRoots.RoundBalance(row.ClosingDebit - row.ClosingCredit),
                    BankAccountId = bankAccountsByChart.TryGetValue(accountNumber, out var id) ? id : null,
                    IsCash = TreasuryAccountRoots.IsCashAccount(accountNumber)
                };
            })
            .OrderBy(a => a.AccountNumber, StringComparer.Ordinal)
            .ToList();

        return new CashPositionDto
        {
            AsOf = date,
            TotalBalance = TreasuryAccountRoots.RoundBalance(accounts.Sum(a => a.Balance)),
            Accounts = accounts
        };
    }

    /// <summary>
    /// Correspondance compte comptable → compte bancaire du référentiel, pour que l'écran puisse
    /// proposer un lien vers le rapprochement. Un compte comptable partagé par plusieurs comptes
    /// bancaires est ignoré : rattacher le solde à l'un d'eux serait arbitraire.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, Guid>> LoadBankAccountsByChartAsync(
        CancellationToken cancellationToken)
    {
        await using var ctx = _contextFactory.CreateContext();

        var rows = await ctx.BankAccounts
            .AsNoTracking()
            .Where(b => b.ChartOfAccountNumber != null && b.ChartOfAccountNumber != "")
            .Select(b => new { b.Id, b.ChartOfAccountNumber })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ChartOfAccountNumber!.Trim(), StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
    }
}
