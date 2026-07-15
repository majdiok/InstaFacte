using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class AccountingReportingService : IAccountingReportingService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly Application.Configuration.AccountingSettings _settings;

    public AccountingReportingService(
        ITenantDbContextFactory contextFactory,
        Microsoft.Extensions.Options.IOptions<Application.Configuration.AccountingSettings> settings)
    {
        _contextFactory = contextFactory;
        _settings = settings.Value;
    }

    /// <summary>
    /// Politique brouillard (C3) : les écrans de consultation (journal, grand livre, balance,
    /// dashboard) incluent les brouillons selon <c>IncludeBrouillardInReports</c> — toujours
    /// marqués via Status/IsDraft. Les états de synthèse (bilan, résultat, NCT) et les sorties
    /// légales (FEC, à-nouveaux, TVA) excluent les brouillons inconditionnellement.
    /// </summary>
    private bool ShowDrafts => _settings.IncludeBrouillardInReports;

    public async Task<Result<IReadOnlyList<ChartOfAccountDto>>> GetChartOfAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var rows = await ctx.ChartOfAccounts.AsNoTracking()
            .OrderBy(c => c.AccountNumber)
            .Select(c => new ChartOfAccountDto
            {
                Id = c.Id,
                AccountNumber = c.AccountNumber,
                Label = c.Label,
                AccountClass = c.AccountClass,
                ParentAccountNumber = c.ParentAccountNumber,
                NatureType = (int)c.NatureType,
                IsSystem = c.IsSystem,
                IsActive = c.IsActive,
                Level = c.Level,
                AccountType = (int)c.AccountType,
                IsAuxiliary = c.IsAuxiliary,
                AffectationAccountNumber = c.AffectationAccountNumber
            })
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ChartOfAccountDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<JournalEntryDto>>> GetJournalEntriesAsync(
        string? journalCode,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var f = from.Date;
        var t = to.Date;
        await using var ctx = _contextFactory.CreateContext();
        var q = ctx.JournalEntries.AsNoTracking()
            .Include(j => j.Lines)
            .Where(j => j.EntryDate >= f && j.EntryDate <= t);
        if (!ShowDrafts)
            q = q.Where(j => j.Status != JournalEntryStatus.Brouillon);
        if (!string.IsNullOrWhiteSpace(journalCode))
        {
            var jc = journalCode.Trim().ToUpperInvariant();
            q = q.Where(j => j.JournalCode == jc);
        }

        var list = await q.OrderByDescending(j => j.EntryDate).ThenByDescending(j => j.EntryNumber).ToListAsync(cancellationToken);

        // Nombre de pièces jointes par écriture, en une seule requête groupée (pas de N+1).
        var entryIds = list.Select(e => e.Id).ToList();
        var attachmentCounts = entryIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await ctx.JournalEntryAttachments.AsNoTracking()
                .Where(a => entryIds.Contains(a.JournalEntryId))
                .GroupBy(a => a.JournalEntryId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var dtos = list
            .Select(e => MapEntry(e) with { AttachmentCount = attachmentCounts.TryGetValue(e.Id, out var c) ? c : 0 })
            .ToList();
        return Result.Success<IReadOnlyList<JournalEntryDto>>(dtos);
    }

    public async Task<Result<IReadOnlyList<LedgerRowDto>>> GetLedgerAsync(
        string accountNumber,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var acc = accountNumber.Trim();
        var f = from.Date;
        var t = to.Date;
        await using var ctx = _contextFactory.CreateContext();

        var lq = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountNumber == acc && l.JournalEntry.EntryDate >= f && l.JournalEntry.EntryDate <= t);
        if (!ShowDrafts)
            lq = lq.Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);

        var lines = await lq
            .OrderBy(l => l.JournalEntry.EntryDate)
            .ThenBy(l => l.JournalEntry.EntryNumber)
            .ThenBy(l => l.LineNumber)
            .ToListAsync(cancellationToken);

        decimal running = 0;
        var rows = new List<LedgerRowDto>();
        foreach (var line in lines)
        {
            var d = line.DebitAmount.Amount;
            var c = line.CreditAmount.Amount;
            running += d - c;
            rows.Add(new LedgerRowDto
            {
                EntryDate = line.JournalEntry.EntryDate,
                JournalCode = line.JournalEntry.JournalCode,
                PieceNumber = line.JournalEntry.EntryNumber,
                Label = line.Label,
                Debit = d,
                Credit = c,
                RunningBalance = running
            });
        }

        return Result.Success<IReadOnlyList<LedgerRowDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<BalanceRowDto>>> GetBalanceAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var f = from.Date;
        var t = to.Date;
        await using var ctx = _contextFactory.CreateContext();

        var openingQuery = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate < f);
        if (!ShowDrafts)
            openingQuery = openingQuery.Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);

        var openingLines = await openingQuery
            .GroupBy(l => l.AccountNumber)
            .Select(g => new
            {
                Account = g.Key,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        var openingMap = openingLines.ToDictionary(x => x.Account, x => (x.Debit, x.Credit));

        // Aggregate movements server-side instead of loading all lines into memory
        var movementQuery = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate >= f && l.JournalEntry.EntryDate <= t);
        if (!ShowDrafts)
            movementQuery = movementQuery.Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);

        var movementLines = await movementQuery
            .GroupBy(l => l.AccountNumber)
            .Select(g => new
            {
                Account = g.Key,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        var movementMap = movementLines.ToDictionary(x => x.Account, x => (x.Debit, x.Credit));

        var labels = await ctx.ChartOfAccounts.AsNoTracking()
            .ToDictionaryAsync(c => c.AccountNumber, c => c.Label, cancellationToken);

        var allAccounts = new HashSet<string>(openingMap.Keys);
        foreach (var m in movementMap.Keys) allAccounts.Add(m);

        var rows = new List<BalanceRowDto>();
        foreach (var acc in allAccounts.OrderBy(x => x))
        {
            openingMap.TryGetValue(acc, out var opening);
            var openNet = opening.Debit - opening.Credit;
            decimal od = 0, oc = 0;
            if (openNet > 0) od = openNet;
            else oc = -openNet;

            movementMap.TryGetValue(acc, out var movement);
            var md = movement.Debit;
            var mc = movement.Credit;

            var closingNet = openNet + md - mc;
            decimal cd = 0, cc = 0;
            if (closingNet > 0) cd = closingNet;
            else cc = -closingNet;

            rows.Add(new BalanceRowDto
            {
                AccountNumber = acc,
                Label = labels.TryGetValue(acc, out var lb) ? lb : acc,
                OpeningDebit = od,
                OpeningCredit = oc,
                MovementDebit = md,
                MovementCredit = mc,
                ClosingDebit = cd,
                ClosingCredit = cc
            });
        }

        return Result.Success<IReadOnlyList<BalanceRowDto>>(rows);
    }

    public async Task<Result<AccountingDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var now = DateTime.UtcNow;
        var year = now.Year;
        var month = now.Month;

        var vatQuery = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l =>
                (l.AccountNumber.StartsWith("43671") || l.AccountNumber.StartsWith("43666") || l.AccountNumber.StartsWith("43667")) &&
                l.JournalEntry.EntryDate.Year == year &&
                l.JournalEntry.EntryDate.Month == month);
        if (!ShowDrafts)
            vatQuery = vatQuery.Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);
        var vatLines = await vatQuery.ToListAsync(cancellationToken);

        decimal collected = vatLines.Where(l => l.AccountNumber.StartsWith("43671")).Sum(l => l.CreditAmount.Amount - l.DebitAmount.Amount);
        decimal ded = vatLines.Where(l => l.AccountNumber.StartsWith("43666")).Sum(l => l.DebitAmount.Amount - l.CreditAmount.Amount);
        decimal cred = vatLines.Where(l => l.AccountNumber.StartsWith("43667")).Sum(l => l.CreditAmount.Amount - l.DebitAmount.Amount);

        var overdue = await ComputeOverdueClientsAsync(ctx, cancellationToken);

        var validatedCount = await ctx.Invoices.AsNoTracking()
            .CountAsync(i => i.Status == InvoiceStatus.Validated, cancellationToken);
        var posted = await ctx.JournalEntries.AsNoTracking()
            .CountAsync(j => j.SourceEntityType == AccountingService.SourceInvoice, cancellationToken);
        var unposted = Math.Max(0, validatedCount - posted);

        var deadline = VatFilingDeadline.ForPeriod(year, month);

        // Aggregate revenue server-side instead of loading all lines
        var monthlyRevenueQuery = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountNumber.StartsWith("7") && l.JournalEntry.EntryDate.Year == year && l.JournalEntry.EntryDate.Month == month);
        if (!ShowDrafts)
            monthlyRevenueQuery = monthlyRevenueQuery.Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);
        var monthlyRevenue = await monthlyRevenueQuery
            .SumAsync(l => l.CreditAmount.Amount - l.DebitAmount.Amount, cancellationToken);

        var prevMonth = month == 1 ? 12 : month - 1;
        var prevYear = month == 1 ? year - 1 : year;
        var previousRevenueQuery = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountNumber.StartsWith("7") && l.JournalEntry.EntryDate.Year == prevYear && l.JournalEntry.EntryDate.Month == prevMonth);
        if (!ShowDrafts)
            previousRevenueQuery = previousRevenueQuery.Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);
        var previousMonthRevenue = await previousRevenueQuery
            .SumAsync(l => l.CreditAmount.Amount - l.DebitAmount.Amount, cancellationToken);

        // Aggregate cash balance server-side (was loading ALL class 5 lines into memory)
        var cashQuery = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountNumber.StartsWith("5"));
        if (!ShowDrafts)
            cashQuery = cashQuery.Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);
        var availableCash = await cashQuery
            .SumAsync(l => l.DebitAmount.Amount - l.CreditAmount.Amount, cancellationToken);

        var openPeriods = await ctx.AccountingPeriods.AsNoTracking().CountAsync(p => !p.IsClosed, cancellationToken);
        var draftEntries = await ctx.JournalEntries.AsNoTracking()
            .CountAsync(j => j.Status == JournalEntryStatus.Brouillon, cancellationToken);

        var dto = new AccountingDashboardDto
        {
            // Journal-based fallback. The authoritative value is overridden by
            // GetAccountingDashboardQueryHandler using the canonical VAT declaration so the
            // dashboard matches the "Déclaration TVA" page exactly.
            VatDueEstimate = collected - ded - cred,
            OverdueReceivablesOver90 = overdue,
            UnpostedInvoiceCount = unposted,
            NextVatDeadline = deadline,
            MonthlyRevenue = monthlyRevenue,
            PreviousMonthRevenue = previousMonthRevenue,
            AvailableCash = availableCash,
            OpenPeriodsCount = openPeriods,
            DraftEntriesCount = draftEntries
        };
        return Result.Success(dto);
    }

    public async Task<Result<IReadOnlyList<AccountingPeriodDto>>> GetPeriodsAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var rows = await ctx.AccountingPeriods.AsNoTracking()
            .OrderByDescending(p => p.FiscalYear).ThenByDescending(p => p.Month)
            .Select(p => new AccountingPeriodDto
            {
                Id = p.Id,
                FiscalYear = p.FiscalYear,
                Month = p.Month,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                IsClosed = p.IsClosed
            })
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AccountingPeriodDto>>(rows);
    }

    public async Task<Result<BalanceSheetDto>> GetBalanceSheetAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var labels = await ctx.ChartOfAccounts.AsNoTracking()
            .ToDictionaryAsync(c => c.AccountNumber, c => (c.Label, c.AccountClass, c.NatureType), cancellationToken);

        // État de synthèse : les brouillons sont exclus inconditionnellement (même style que
        // LoadYearNetAsync pour les états NCT).
        var currentYearBalances = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == fiscalYear
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.AccountNumber)
            .Select(g => new
            {
                Account = g.Key,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        var previousYear = fiscalYear - 1;
        var previousYearBalances = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == previousYear
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.AccountNumber)
            .Select(g => new
            {
                Account = g.Key,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        var prevMap = previousYearBalances.ToDictionary(x => x.Account, x => x.Debit - x.Credit);

        var assets = new List<FinancialStatementLineDto>();
        var liabilities = new List<FinancialStatementLineDto>();

        foreach (var bal in currentYearBalances)
        {
            var net = bal.Debit - bal.Credit;
            if (net == 0) continue;

            labels.TryGetValue(bal.Account, out var info);
            var accountClass = info.AccountClass;
            var label = info.Label ?? bal.Account;

            // Côté du bilan : la classe 4 (comptes de tiers, nature mixte) est ventilée selon
            // la nature du compte ; les autres classes selon leur classe. Les classes 6 et 7
            // (comptes de gestion) sont ignorées — le résultat ressort comme l'écart Actif − Passif.
            bool isAsset;
            if (accountClass is 2 or 3 or 5)
                isAsset = true;
            else if (accountClass == 1)
                isAsset = false;
            else if (accountClass == 4)
                isAsset = info.NatureType == AccountNatureType.Debit;
            else
                continue;

            var hasPrev = prevMap.TryGetValue(bal.Account, out var prevNet);

            // Montant signé selon le côté : un compte d'actif présente Débit − Crédit, un compte
            // de passif présente Crédit − Débit. Un solde anormal ressort en négatif, ce qui
            // préserve l'équilibre du bilan (Total Actif − Total Passif = résultat de l'exercice).
            var line = new FinancialStatementLineDto
            {
                AccountNumber = bal.Account,
                Label = label,
                AccountClass = accountClass,
                Amount = isAsset ? net : -net,
                PreviousYearAmount = hasPrev ? (isAsset ? prevNet : -prevNet) : null
            };

            if (isAsset)
                assets.Add(line);
            else
                liabilities.Add(line);
        }

        var totalAssets = assets.Sum(a => a.Amount);
        var totalLiabilities = liabilities.Sum(l => l.Amount);

        return Result.Success(new BalanceSheetDto
        {
            Assets = assets.OrderBy(a => a.AccountNumber).ToList(),
            Liabilities = liabilities.OrderBy(l => l.AccountNumber).ToList(),
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            NetResult = totalAssets - totalLiabilities
        });
    }

    public async Task<Result<IncomeStatementDto>> GetIncomeStatementAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var labels = await ctx.ChartOfAccounts.AsNoTracking()
            .ToDictionaryAsync(c => c.AccountNumber, c => (c.Label, c.AccountClass), cancellationToken);

        // État de synthèse : brouillons exclus inconditionnellement.
        var balances = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == fiscalYear
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.AccountNumber)
            .Select(g => new
            {
                Account = g.Key,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        var previousYear = fiscalYear - 1;
        var prevBalances = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == previousYear
                        && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.AccountNumber)
            .Select(g => new
            {
                Account = g.Key,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        var prevMap = prevBalances.ToDictionary(x => x.Account);

        var revenue = new List<FinancialStatementLineDto>();
        var expenses = new List<FinancialStatementLineDto>();

        foreach (var bal in balances)
        {
            labels.TryGetValue(bal.Account, out var info);
            var accountClass = info.AccountClass;
            var label = info.Label ?? bal.Account;

            if (accountClass == 7)
            {
                var amount = bal.Credit - bal.Debit;
                decimal? prevAmount = prevMap.TryGetValue(bal.Account, out var prev)
                    ? prev.Credit - prev.Debit
                    : null;

                revenue.Add(new FinancialStatementLineDto
                {
                    AccountNumber = bal.Account,
                    Label = label,
                    AccountClass = accountClass,
                    Amount = amount,
                    PreviousYearAmount = prevAmount
                });
            }
            else if (accountClass == 6)
            {
                var amount = bal.Debit - bal.Credit;
                decimal? prevAmount = prevMap.TryGetValue(bal.Account, out var prev)
                    ? prev.Debit - prev.Credit
                    : null;

                expenses.Add(new FinancialStatementLineDto
                {
                    AccountNumber = bal.Account,
                    Label = label,
                    AccountClass = accountClass,
                    Amount = amount,
                    PreviousYearAmount = prevAmount
                });
            }
        }

        var totalRevenue = revenue.Sum(r => r.Amount);
        var totalExpenses = expenses.Sum(e => e.Amount);

        return Result.Success(new IncomeStatementDto
        {
            Revenue = revenue.OrderBy(r => r.AccountNumber).ToList(),
            Expenses = expenses.OrderBy(e => e.AccountNumber).ToList(),
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            NetResult = totalRevenue - totalExpenses
        });
    }

    public async Task<Result<NctFinancialStatementsDto>> GetNctStatementsAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var current = await LoadYearNetAsync(ctx, fiscalYear, cancellationToken);
        var previous = await LoadYearNetAsync(ctx, fiscalYear - 1, cancellationToken);

        var dto = NctStatementBuilder.Build(fiscalYear, current, previous, _settings.NctStatementsEnabled);
        return Result.Success(dto);
    }

    public async Task<Result<IReadOnlyList<AuxiliaryBalanceRowDto>>> GetAuxiliaryBalanceAsync(
        ThirdPartyKind kind, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var f = from.Date;
        var t = to.Date;
        await using var ctx = _contextFactory.CreateContext();

        IQueryable<JournalEntryLine> Base() =>
            ShowDrafts
                ? ctx.JournalEntryLines.AsNoTracking()
                    .Include(l => l.JournalEntry)
                    .Where(l => l.ThirdPartyId != null && l.ThirdPartyKind == kind)
                : ctx.JournalEntryLines.AsNoTracking()
                    .Include(l => l.JournalEntry)
                    .Where(l => l.ThirdPartyId != null && l.ThirdPartyKind == kind
                                && l.JournalEntry.Status != JournalEntryStatus.Brouillon);

        var opening = await Base()
            .Where(l => l.JournalEntry.EntryDate < f)
            .GroupBy(l => l.ThirdPartyId!.Value)
            .Select(g => new { Id = g.Key, Debit = g.Sum(l => l.DebitAmount.Amount), Credit = g.Sum(l => l.CreditAmount.Amount) })
            .ToListAsync(cancellationToken);

        var movements = await Base()
            .Where(l => l.JournalEntry.EntryDate >= f && l.JournalEntry.EntryDate <= t)
            .GroupBy(l => l.ThirdPartyId!.Value)
            .Select(g => new { Id = g.Key, Debit = g.Sum(l => l.DebitAmount.Amount), Credit = g.Sum(l => l.CreditAmount.Amount) })
            .ToListAsync(cancellationToken);

        var openingMap = opening.ToDictionary(x => x.Id, x => (x.Debit, x.Credit));
        var movementMap = movements.ToDictionary(x => x.Id, x => (x.Debit, x.Credit));

        var ids = new HashSet<Guid>(openingMap.Keys);
        foreach (var id in movementMap.Keys) ids.Add(id);

        var names = await LoadThirdPartyNamesAsync(ctx, kind, ids, cancellationToken);

        var rows = new List<AuxiliaryBalanceRowDto>();
        foreach (var id in ids)
        {
            openingMap.TryGetValue(id, out var op);
            movementMap.TryGetValue(id, out var mv);

            var openNet = op.Debit - op.Credit;
            decimal od = 0, oc = 0;
            if (openNet > 0) od = openNet; else oc = -openNet;

            var closingNet = openNet + mv.Debit - mv.Credit;
            decimal cd = 0, cc = 0;
            if (closingNet > 0) cd = closingNet; else cc = -closingNet;

            rows.Add(new AuxiliaryBalanceRowDto
            {
                ThirdPartyId = id,
                ThirdPartyName = names.TryGetValue(id, out var n) ? n : id.ToString("N")[..8],
                OpeningDebit = od,
                OpeningCredit = oc,
                MovementDebit = mv.Debit,
                MovementCredit = mv.Credit,
                ClosingDebit = cd,
                ClosingCredit = cc
            });
        }

        return Result.Success<IReadOnlyList<AuxiliaryBalanceRowDto>>(
            rows.OrderBy(r => r.ThirdPartyName, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<Result<ThirdPartyLedgerDto>> GetThirdPartyLedgerAsync(
        Guid thirdPartyId, ThirdPartyKind kind, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var f = from.Date;
        var t = to.Date;
        await using var ctx = _contextFactory.CreateContext();

        IQueryable<JournalEntryLine> Base() =>
            ShowDrafts
                ? ctx.JournalEntryLines.AsNoTracking()
                    .Include(l => l.JournalEntry)
                    .Where(l => l.ThirdPartyId == thirdPartyId && l.ThirdPartyKind == kind)
                : ctx.JournalEntryLines.AsNoTracking()
                    .Include(l => l.JournalEntry)
                    .Where(l => l.ThirdPartyId == thirdPartyId && l.ThirdPartyKind == kind
                                && l.JournalEntry.Status != JournalEntryStatus.Brouillon);

        var openingBalance = await Base()
            .Where(l => l.JournalEntry.EntryDate < f)
            .SumAsync(l => l.DebitAmount.Amount - l.CreditAmount.Amount, cancellationToken);

        var lines = await Base()
            .Where(l => l.JournalEntry.EntryDate >= f && l.JournalEntry.EntryDate <= t)
            .OrderBy(l => l.JournalEntry.EntryDate)
            .ThenBy(l => l.JournalEntry.EntryNumber)
            .ThenBy(l => l.LineNumber)
            .ToListAsync(cancellationToken);

        var running = openingBalance;
        var rows = new List<ThirdPartyLedgerRowDto>(lines.Count);
        foreach (var line in lines)
        {
            running += line.DebitAmount.Amount - line.CreditAmount.Amount;
            rows.Add(new ThirdPartyLedgerRowDto
            {
                EntryDate = line.JournalEntry.EntryDate,
                JournalCode = line.JournalEntry.JournalCode,
                PieceNumber = line.JournalEntry.EntryNumber,
                PieceRef = line.JournalEntry.PieceRef,
                AccountNumber = line.AccountNumber,
                Label = line.Label,
                Debit = line.DebitAmount.Amount,
                Credit = line.CreditAmount.Amount,
                RunningBalance = running,
                LetteringCode = line.LetteringCode
            });
        }

        var names = await LoadThirdPartyNamesAsync(ctx, kind, new HashSet<Guid> { thirdPartyId }, cancellationToken);

        return Result.Success(new ThirdPartyLedgerDto
        {
            ThirdPartyId = thirdPartyId,
            ThirdPartyName = names.TryGetValue(thirdPartyId, out var n) ? n : thirdPartyId.ToString("N")[..8],
            OpeningBalance = openingBalance,
            Rows = rows
        });
    }

    /// <summary>Noms des tiers chargés en lot (pattern FecExportService) — jamais de N+1.</summary>
    private static async Task<Dictionary<Guid, string>> LoadThirdPartyNamesAsync(
        Persistence.TenantDbContext ctx, ThirdPartyKind kind, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return kind == ThirdPartyKind.Client
            ? await ctx.Clients.AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : await ctx.Suppliers.AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
    }

    public async Task<Result<IReadOnlyList<JournalSearchRowDto>>> SearchJournalEntriesAsync(
        string? accountNumber, string? journalCode, DateTime? from, DateTime? to,
        decimal? minAmount, decimal? maxAmount, string? label, string? letteringCode, int? status, int take,
        string? pieceRef = null,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var q = ctx.JournalEntryLines.AsNoTracking().Include(l => l.JournalEntry).AsQueryable();

        if (!string.IsNullOrWhiteSpace(accountNumber))
        {
            var acc = accountNumber.Trim();
            q = q.Where(l => l.AccountNumber.StartsWith(acc));
        }
        if (!string.IsNullOrWhiteSpace(journalCode))
        {
            var jc = journalCode.Trim().ToUpperInvariant();
            q = q.Where(l => l.JournalEntry.JournalCode == jc);
        }
        if (from.HasValue) { var f = from.Value.Date; q = q.Where(l => l.JournalEntry.EntryDate >= f); }
        if (to.HasValue) { var t = to.Value.Date; q = q.Where(l => l.JournalEntry.EntryDate <= t); }
        if (!string.IsNullOrWhiteSpace(label))
        {
            var lbl = label.Trim();
            q = q.Where(l => l.Label.Contains(lbl));
        }
        if (!string.IsNullOrWhiteSpace(letteringCode))
        {
            var lc = letteringCode.Trim();
            q = q.Where(l => l.LetteringCode == lc);
        }
        if (!string.IsNullOrWhiteSpace(pieceRef))
        {
            var pr = pieceRef.Trim();
            q = q.Where(l => l.JournalEntry.PieceRef != null && l.JournalEntry.PieceRef.Contains(pr));
        }
        if (status.HasValue)
        {
            var st = (JournalEntryStatus)status.Value;
            q = q.Where(l => l.JournalEntry.Status == st);
        }
        if (minAmount.HasValue)
        {
            var mn = minAmount.Value;
            q = q.Where(l => l.DebitAmount.Amount >= mn || l.CreditAmount.Amount >= mn);
        }
        if (maxAmount.HasValue)
        {
            var mx = maxAmount.Value;
            q = q.Where(l => (l.DebitAmount.Amount > 0 && l.DebitAmount.Amount <= mx)
                          || (l.CreditAmount.Amount > 0 && l.CreditAmount.Amount <= mx));
        }

        var lim = take <= 0 ? 200 : Math.Min(take, 1000);
        var rows = await q
            .OrderByDescending(l => l.JournalEntry.EntryDate)
            .ThenByDescending(l => l.JournalEntry.EntryNumber)
            .ThenBy(l => l.LineNumber)
            .Take(lim)
            .Select(l => new JournalSearchRowDto
            {
                EntryId = l.JournalEntryId,
                LineId = l.Id,
                EntryDate = l.JournalEntry.EntryDate,
                JournalCode = l.JournalEntry.JournalCode,
                EntryNumber = l.JournalEntry.EntryNumber,
                AccountNumber = l.AccountNumber,
                Label = l.Label,
                Debit = l.DebitAmount.Amount,
                Credit = l.CreditAmount.Amount,
                LetteringCode = l.LetteringCode,
                Status = (int)l.JournalEntry.Status,
                IsDraft = l.JournalEntry.Status == JournalEntryStatus.Brouillon,
                PieceRef = l.JournalEntry.PieceRef
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<JournalSearchRowDto>>(rows);
    }

    /// <summary>Solde net (débit − crédit) par compte pour un exercice, sur écritures VALIDÉES (hors brouillard).</summary>
    private static async Task<Dictionary<string, decimal>> LoadYearNetAsync(
        Persistence.TenantDbContext ctx, int fiscalYear, CancellationToken ct)
    {
        var rows = await ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate.Year == fiscalYear && l.JournalEntry.Status != JournalEntryStatus.Brouillon)
            .GroupBy(l => l.AccountNumber)
            .Select(g => new
            {
                Account = g.Key,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Account, r => r.Debit - r.Credit);
    }

    private static async Task<decimal> ComputeOverdueClientsAsync(Persistence.TenantDbContext ctx, CancellationToken ct)
    {
        var invoices = await ctx.Invoices.AsNoTracking()
            .Include(i => i.Client)
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Paid)
            .ToListAsync(ct);

        var ids = invoices.Select(i => i.Id).ToList();
        if (ids.Count == 0)
            return 0;

        var paidMap = await ctx.Payments.AsNoTracking()
            .Where(p => ids.Contains(p.InvoiceId) && !p.IsRefunded)
            .GroupBy(p => p.InvoiceId)
            .Select(g => new
            {
                InvoiceId = g.Key,
                Total = g.Sum(p => p.Amount.Amount + (p.ClientWithholdingAmount ?? 0m))
            })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Total, ct);

        var today = DateTime.UtcNow.Date;
        decimal sum = 0;
        foreach (var inv in invoices)
        {
            paidMap.TryGetValue(inv.Id, out var paid);
            var balance = inv.TotalAmount.Amount - paid;
            if (balance <= 0 || !inv.DueDate.HasValue)
                continue;
            var days = (today - inv.DueDate.Value.Date).Days;
            if (days > 90)
                sum += balance;
        }

        return sum;
    }

    private static JournalEntryDto MapEntry(JournalEntry j) => new()
    {
        Id = j.Id,
        EntryNumber = j.EntryNumber,
        JournalCode = j.JournalCode,
        EntryDate = j.EntryDate,
        Label = j.Label,
        SourceEntityType = j.SourceEntityType,
        SourceEntityId = j.SourceEntityId,
        IsAutoGenerated = j.IsAutoGenerated,
        IsReversed = j.IsReversed,
        Status = (int)j.Status,
        IsDraft = j.Status == JournalEntryStatus.Brouillon,
        PieceRef = j.PieceRef,
        PieceDate = j.PieceDate,
        Lines = j.Lines.OrderBy(l => l.LineNumber).Select(l => new JournalEntryLineDto
        {
            Id = l.Id,
            LineNumber = l.LineNumber,
            AccountNumber = l.AccountNumber,
            Label = l.Label,
            Debit = l.DebitAmount.Amount,
            Credit = l.CreditAmount.Amount,
            Currency = l.DebitAmount.Amount > 0 ? l.DebitAmount.Currency : l.CreditAmount.Currency,
            LetteringCode = l.LetteringCode,
            ThirdPartyId = l.ThirdPartyId,
            ThirdPartyKind = (int)l.ThirdPartyKind
        }).ToList()
    };

    // ── État budgétaire (budget vs réalisé) ─────────────────────────────────

    public async Task<Result<BudgetReportDto>> GetBudgetReportAsync(
        int fiscalYear, int? throughMonth, CancellationToken cancellationToken = default)
    {
        if (!_settings.BudgetingEnabled)
            return Result.Failure<BudgetReportDto>(Error.Validation(
                "Budgeting", "La comptabilité budgétaire n'est pas activée."));
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure<BudgetReportDto>(Error.Validation("FiscalYear", "Exercice invalide."));
        var lastMonth = throughMonth is >= 1 and <= 12 ? throughMonth.Value : 12;

        await using var ctx = _contextFactory.CreateContext();

        var posts = await ctx.BudgetPosts.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);

        var year = await ctx.BudgetYears.AsNoTracking()
            .FirstOrDefaultAsync(y => y.FiscalYear == fiscalYear, cancellationToken);
        var isValidated = year?.Status == BudgetYearStatus.Validated;

        var budgetLines = await ctx.BudgetLines.AsNoTracking()
            .Where(l => l.FiscalYear == fiscalYear)
            .ToListAsync(cancellationToken);

        // Réalisé : une seule requête, agrégée par compte + mois (mêmes règles brouillard que la balance).
        var from = new DateTime(fiscalYear, 1, 1);
        var to = new DateTime(fiscalYear, 12, 31);
        var linesQuery = ctx.JournalEntryLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to);
        if (!ShowDrafts)
            linesQuery = linesQuery.Where(l => l.JournalEntry.Status != JournalEntryStatus.Brouillon);

        var actualByAccountMonth = await linesQuery
            .GroupBy(l => new { l.AccountNumber, l.JournalEntry.EntryDate.Month })
            .Select(g => new
            {
                g.Key.AccountNumber,
                g.Key.Month,
                Debit = g.Sum(l => l.DebitAmount.Amount),
                Credit = g.Sum(l => l.CreditAmount.Amount)
            })
            .ToListAsync(cancellationToken);

        // Affectation compte → poste : le préfixe le plus long gagne (un compte compté une fois).
        var prefixToPost = new List<(string Prefix, BudgetPost Post)>();
        foreach (var post in posts)
            foreach (var prefix in post.GetPrefixes())
                prefixToPost.Add((prefix, post));
        prefixToPost.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));

        BudgetPost? ResolvePost(string account)
        {
            foreach (var (prefix, post) in prefixToPost)
                if (account.StartsWith(prefix, StringComparison.Ordinal))
                    return post;
            return null;
        }

        var actualByPost = posts.ToDictionary(p => p.Id, _ => new decimal[12]);
        var offPostExpense = new decimal[12];
        var offPostRevenue = new decimal[12];
        foreach (var cell in actualByAccountMonth)
        {
            var post = ResolvePost(cell.AccountNumber);
            if (post is not null)
            {
                var signed = post.Kind == BudgetPostKind.Expense
                    ? cell.Debit - cell.Credit
                    : cell.Credit - cell.Debit;
                actualByPost[post.Id][cell.Month - 1] += signed;
            }
            else if (cell.AccountNumber.StartsWith('6'))
            {
                offPostExpense[cell.Month - 1] += cell.Debit - cell.Credit;
            }
            else if (cell.AccountNumber.StartsWith('7'))
            {
                offPostRevenue[cell.Month - 1] += cell.Credit - cell.Debit;
            }
            // Autres classes non couvertes par un préfixe : hors périmètre budgétaire.
        }

        var budgetByPost = budgetLines.ToLookup(l => l.BudgetPostId);
        var rows = new List<BudgetReportRowDto>(posts.Count + 2);
        foreach (var post in posts)
        {
            var initial = new decimal[12];
            var revised = new decimal[12];
            foreach (var line in budgetByPost[post.Id])
            {
                if (line.Version == BudgetVersion.Initial) initial[line.Month - 1] = line.Amount;
                else revised[line.Month - 1] = line.Amount;
            }
            // Tant que l'initial n'est pas validé, le « révisé » affiché = l'initial.
            if (!isValidated)
                revised = initial;

            rows.Add(BuildRow(post.Code, post.Label, (int)post.Kind, isOffPost: false,
                initial, revised, actualByPost[post.Id], lastMonth));
        }

        if (offPostExpense.Any(v => v != 0m))
            rows.Add(BuildRow("—", "Hors postes (charges)", (int)BudgetPostKind.Expense, isOffPost: true,
                new decimal[12], new decimal[12], offPostExpense, lastMonth));
        if (offPostRevenue.Any(v => v != 0m))
            rows.Add(BuildRow("—", "Hors postes (produits)", (int)BudgetPostKind.Revenue, isOffPost: true,
                new decimal[12], new decimal[12], offPostRevenue, lastMonth));

        var totals = new BudgetReportTotalsDto
        {
            ExpenseInitial = rows.Where(r => r.Kind == (int)BudgetPostKind.Expense).Sum(r => r.PeriodInitial),
            ExpenseRevised = rows.Where(r => r.Kind == (int)BudgetPostKind.Expense).Sum(r => r.PeriodRevised),
            ExpenseActual = rows.Where(r => r.Kind == (int)BudgetPostKind.Expense).Sum(r => r.PeriodActual),
            RevenueInitial = rows.Where(r => r.Kind == (int)BudgetPostKind.Revenue).Sum(r => r.PeriodInitial),
            RevenueRevised = rows.Where(r => r.Kind == (int)BudgetPostKind.Revenue).Sum(r => r.PeriodRevised),
            RevenueActual = rows.Where(r => r.Kind == (int)BudgetPostKind.Revenue).Sum(r => r.PeriodActual)
        };

        return Result.Success(new BudgetReportDto
        {
            FiscalYear = fiscalYear,
            ThroughMonth = lastMonth,
            IsValidated = isValidated,
            Rows = rows,
            Totals = totals
        });
    }

    private static BudgetReportRowDto BuildRow(string code, string label, int kind, bool isOffPost,
        decimal[] initial, decimal[] revised, decimal[] actual, int lastMonth)
    {
        var periodInitial = initial.Take(lastMonth).Sum();
        var periodRevised = revised.Take(lastMonth).Sum();
        var periodActual = actual.Take(lastMonth).Sum();
        return new BudgetReportRowDto
        {
            Code = code,
            Label = label,
            Kind = kind,
            IsOffPost = isOffPost,
            MonthlyInitial = initial,
            MonthlyRevised = revised,
            MonthlyActual = actual,
            PeriodInitial = periodInitial,
            PeriodRevised = periodRevised,
            PeriodActual = periodActual,
            Variance = periodActual - periodRevised,
            ConsumptionPercent = periodRevised != 0m
                ? Math.Round(periodActual / periodRevised * 100m, 1)
                : null
        };
    }
}
