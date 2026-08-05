using System.Data;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class LetteringService : ILetteringService
{
    private readonly ITenantDbContextFactory _contextFactory;

    public LetteringService(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Result> ManualLetterAsync(IReadOnlyList<Guid> journalEntryLineIds, bool allowPartial = false, CancellationToken cancellationToken = default)
    {
        if (journalEntryLineIds.Count < 2)
            return Result.Failure(Error.Validation("Lines", "Au moins deux lignes d'écriture sont requises pour le lettrage."));

        await using var strategyContext = _contextFactory.CreateIsolatedContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var ctx = _contextFactory.CreateIsolatedContext();
            var lines = await ctx.JournalEntryLines
                .AsTracking()
                .Where(l => journalEntryLineIds.Contains(l.Id))
                .ToListAsync(cancellationToken);

            if (lines.Count != journalEntryLineIds.Count)
                return Result.Failure(Error.Validation("JournalEntryLine", "Une ou plusieurs lignes sont introuvables."));

            if (lines.Any(l => !string.IsNullOrEmpty(l.LetteringCode)))
                return Result.Failure(Error.Validation("Lettering", "Une ou plusieurs lignes sont déjà lettrées."));

            var account = lines[0].AccountNumber;
            if (lines.Any(l => l.AccountNumber != account))
                return Result.Failure(Error.Validation("AccountNumber", "Toutes les lignes doivent être sur le même compte."));

            var debit = lines.Sum(l => l.DebitAmount.Amount);
            var credit = lines.Sum(l => l.CreditAmount.Amount);
            var balanced = Math.Round(debit, 3) == Math.Round(credit, 3);
            if (!balanced && !allowPartial)
                return Result.Failure(Error.Validation("Balance",
                    "Le lettrage nécessite une égalité débit / crédit (ou cochez « lettrage partiel »)."));

            // Un « partiel » équilibré est un lettrage définitif ordinaire.
            var isPartial = !balanced;

            var currency = lines[0].DebitAmount.Currency;
            if (lines.Any(l => l.DebitAmount.Currency != currency || l.CreditAmount.Currency != currency))
                return Result.Failure(Error.Validation("Currency", "Les lignes doivent partager la même devise."));

            await using var transaction = ctx.Database.IsRelational()
                ? await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var code = await NextCodeAsync(ctx, isPartial, cancellationToken);

                // Montant lettré : total équilibré, ou partie couverte (min) pour un partiel.
                var amount = isPartial ? Math.Min(debit, credit) : debit;
                var create = LetteringGroup.Create(
                    code,
                    account,
                    Money.Create(amount, currency),
                    journalEntryLineIds.ToList(),
                    isPartial);

                if (create.IsFailure)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(create.Error);
                }

                var group = create.Value;
                group.SetAuditInfo("system", false);
                ctx.LetteringGroups.Add(group);

                foreach (var line in lines)
                    line.SetLetteringCode(code);

                await ctx.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Result> UnletterAsync(string code, CancellationToken cancellationToken = default)
    {
        code = code?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(code))
            return Result.Failure(Error.Validation("Code", "Le code de lettrage est obligatoire."));

        await using var ctx = _contextFactory.CreateContext();
        var group = await ctx.LetteringGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Code == code, cancellationToken);
        if (group is null)
            return Result.Failure(Error.Validation("Code", $"Aucun groupe de lettrage « {code} » n'existe."));

        var lineIds = group.Members.Select(m => m.JournalEntryLineId).ToList();
        var lines = await ctx.JournalEntryLines
            .AsTracking()
            .Where(l => lineIds.Contains(l.Id))
            .ToListAsync(cancellationToken);

        foreach (var line in lines)
            line.SetLetteringCode(null);

        // Membres supprimés en cascade avec le groupe ; un seul SaveChanges = atomique.
        ctx.LetteringGroups.Remove(group);
        await ctx.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Prochain code de lettrage : max de la partie numérique des codes existants + 1 (préfixe
    /// « L » définitif, « P » partiel). L'ancien schéma « Count + 1 » collisionnait dès qu'un
    /// groupe était supprimé par délettrage.
    /// </summary>
    private static async Task<string> NextCodeAsync(Persistence.TenantDbContext ctx, bool isPartial, CancellationToken cancellationToken)
    {
        var codes = await ctx.LetteringGroups
            .AsNoTracking()
            .Select(g => g.Code)
            .ToListAsync(cancellationToken);

        var next = codes
            .Select(c => c.Length > 1 && int.TryParse(c.AsSpan(1), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{(isPartial ? 'P' : 'L')}{next:D5}";
    }

    public async Task<Result> AutoLetterPaymentAsync(
        string sourceEntityType,
        Guid sourceEntityId,
        string invoiceSourceType,
        Guid invoiceSourceId,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        // Find the payment journal entry
        var paymentEntry = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.SourceEntityType == sourceEntityType && j.SourceEntityId == sourceEntityId, cancellationToken);

        if (paymentEntry is null)
            return Result.Success(); // No entry yet, skip silently

        // Find the invoice journal entry
        var invoiceEntry = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.SourceEntityType == invoiceSourceType && j.SourceEntityId == invoiceSourceId, cancellationToken);

        if (invoiceEntry is null)
            return Result.Success();

        // Find matching third-party account lines (e.g. 4111 for client, 4011 for supplier)
        // The invoice line will be a debit on 4111, the payment line will be a credit on 4111
        var invoiceThirdPartyLines = invoiceEntry.Lines
            .Where(l => l.ThirdPartyId.HasValue && string.IsNullOrEmpty(l.LetteringCode))
            .ToList();

        var paymentThirdPartyLines = paymentEntry.Lines
            .Where(l => l.ThirdPartyId.HasValue && string.IsNullOrEmpty(l.LetteringCode))
            .ToList();

        // Try to match lines on the same account
        foreach (var invoiceLine in invoiceThirdPartyLines)
        {
            var matchingPaymentLine = paymentThirdPartyLines
                .FirstOrDefault(p => p.AccountNumber == invoiceLine.AccountNumber);

            if (matchingPaymentLine is null)
                continue;

            // Check if debits and credits balance
            var totalDebit = invoiceLine.DebitAmount.Amount + matchingPaymentLine.DebitAmount.Amount;
            var totalCredit = invoiceLine.CreditAmount.Amount + matchingPaymentLine.CreditAmount.Amount;

            if (Math.Round(totalDebit, 3) != Math.Round(totalCredit, 3))
                continue; // Not balanced — partial payment, skip

            // Letter these two lines
            var lineIds = new List<Guid> { invoiceLine.Id, matchingPaymentLine.Id };
            var result = await ManualLetterAsync(lineIds, allowPartial: false, cancellationToken);
            if (result.IsFailure)
            {
                // Silently skip — auto-lettering is best-effort
                continue;
            }
        }

        return Result.Success();
    }

    public async Task<Result> AutoLetterPayrollPaymentAsync(
        Guid payrollPaymentId,
        Guid payrollRunId,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var paymentEntry = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(
                j => j.SourceEntityType == AccountingService.SourcePayrollPayment
                     && j.SourceEntityId == payrollPaymentId,
                cancellationToken);

        if (paymentEntry is null)
            return Result.Success();

        var payrollEntry = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(
                j => j.SourceEntityType == AccountingService.SourcePayrollRun
                     && j.SourceEntityId == payrollRunId,
                cancellationToken);

        if (payrollEntry is null)
            return Result.Success();

        var payrollCredits = payrollEntry.Lines
            .Where(l => l.ThirdPartyKind == Domain.Enums.ThirdPartyKind.Employee
                        && l.CreditAmount.Amount > 0
                        && string.IsNullOrEmpty(l.LetteringCode))
            .ToList();

        var paymentDebits = paymentEntry.Lines
            .Where(l => l.ThirdPartyKind == Domain.Enums.ThirdPartyKind.Employee
                        && l.DebitAmount.Amount > 0
                        && string.IsNullOrEmpty(l.LetteringCode))
            .ToList();

        foreach (var creditLine in payrollCredits)
        {
            var debitLine = paymentDebits.FirstOrDefault(d =>
                d.AccountNumber == creditLine.AccountNumber
                && d.ThirdPartyId == creditLine.ThirdPartyId);

            if (debitLine is null)
                continue;

            var totalDebit = creditLine.DebitAmount.Amount + debitLine.DebitAmount.Amount;
            var totalCredit = creditLine.CreditAmount.Amount + debitLine.CreditAmount.Amount;
            var balanced = Math.Round(totalDebit, 3) == Math.Round(totalCredit, 3);

            var lineIds = new List<Guid> { creditLine.Id, debitLine.Id };
            await ManualLetterAsync(lineIds, allowPartial: !balanced, cancellationToken);
        }

        // Legacy aggregated 421 line (no employee third party) — partial lettering when enabled.
        var legacyCredit = payrollEntry.Lines
            .FirstOrDefault(l => l.AccountNumber.StartsWith("421", StringComparison.Ordinal)
                                 && l.CreditAmount.Amount > 0
                                 && l.ThirdPartyKind == Domain.Enums.ThirdPartyKind.None
                                 && string.IsNullOrEmpty(l.LetteringCode));

        if (legacyCredit is not null)
        {
            var employeeDebits = paymentDebits
                .Where(d => string.IsNullOrEmpty(d.LetteringCode))
                .ToList();
            if (employeeDebits.Count > 0)
            {
                var lineIds = new List<Guid> { legacyCredit.Id };
                lineIds.AddRange(employeeDebits.Select(d => d.Id));
                var totalDebit = legacyCredit.DebitAmount.Amount + employeeDebits.Sum(d => d.DebitAmount.Amount);
                var totalCredit = legacyCredit.CreditAmount.Amount + employeeDebits.Sum(d => d.CreditAmount.Amount);
                var balanced = Math.Round(totalDebit, 3) == Math.Round(totalCredit, 3);
                await ManualLetterAsync(lineIds, allowPartial: !balanced, cancellationToken);
            }
        }

        return Result.Success();
    }

    public async Task<Result> UnletterPayrollPaymentAsync(Guid payrollPaymentId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var paymentEntry = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(
                j => j.SourceEntityType == AccountingService.SourcePayrollPayment
                     && j.SourceEntityId == payrollPaymentId,
                cancellationToken);

        if (paymentEntry is null)
            return Result.Success();

        var codes = paymentEntry.Lines
            .Select(l => l.LetteringCode)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();

        foreach (var code in codes)
        {
            var result = await UnletterAsync(code!, cancellationToken);
            if (result.IsFailure)
                return result;
        }

        return Result.Success();
    }
}
