using System.Data;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class LetteringService : ILetteringService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly TenantAmbientTransaction _ambient;

    public LetteringService(ITenantDbContextFactory contextFactory, TenantAmbientTransaction ambient)
    {
        _contextFactory = contextFactory;
        _ambient = ambient;
    }

    public async Task<Result> ManualLetterAsync(IReadOnlyList<Guid> journalEntryLineIds, bool allowPartial = false, CancellationToken cancellationToken = default)
    {
        if (journalEntryLineIds.Count < 2)
            return Result.Failure(Error.Validation("Lines", "Au moins deux lignes d'écriture sont requises pour le lettrage."));

        if (_ambient.IsActive)
        {
            await using var ctx = _contextFactory.CreateContext();
            return await LetterInContextAsync(ctx, journalEntryLineIds, allowPartial, saveChanges: true, cancellationToken);
        }

        await using var strategyContext = _contextFactory.CreateIsolatedContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var ctx = _contextFactory.CreateIsolatedContext();
            var validation = await ValidateLinesAsync(ctx, journalEntryLineIds, allowPartial, cancellationToken);
            if (validation.IsFailure)
                return Result.Failure(validation.Error);

            await using var transaction = ctx.Database.IsRelational()
                ? await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var result = await LetterInContextAsync(
                    ctx,
                    journalEntryLineIds,
                    allowPartial,
                    saveChanges: true,
                    cancellationToken,
                    validation.Value);

                if (result.IsFailure)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return result;
                }

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

    private async Task<Result> LetterInContextAsync(
        TenantDbContext ctx,
        IReadOnlyList<Guid> journalEntryLineIds,
        bool allowPartial,
        bool saveChanges,
        CancellationToken cancellationToken,
        LetteringValidation? prevalidated = null)
    {
        LetteringValidation validation;
        if (prevalidated is not null)
        {
            validation = prevalidated;
        }
        else
        {
            var validationResult = await ValidateLinesAsync(ctx, journalEntryLineIds, allowPartial, cancellationToken);
            if (validationResult.IsFailure)
                return validationResult;
            validation = validationResult.Value;
        }

        var (lines, account, isPartial, currency, debit, credit) = validation;

        var code = await NextCodeAsync(ctx, isPartial, cancellationToken);
        var amount = isPartial ? Math.Min(debit, credit) : debit;
        var create = LetteringGroup.Create(
            code,
            account,
            Money.Create(amount, currency),
            journalEntryLineIds.ToList(),
            isPartial);

        if (create.IsFailure)
            return Result.Failure(create.Error);

        var group = create.Value;
        group.SetAuditInfo("system", false);
        ctx.LetteringGroups.Add(group);

        foreach (var line in lines)
            line.SetLetteringCode(code);

        if (saveChanges)
            await ctx.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static async Task<Result<LetteringValidation>> ValidateLinesAsync(
        TenantDbContext ctx,
        IReadOnlyList<Guid> journalEntryLineIds,
        bool allowPartial,
        CancellationToken cancellationToken)
    {
        var lines = await ctx.JournalEntryLines
            .AsTracking()
            .Where(l => journalEntryLineIds.Contains(l.Id))
            .ToListAsync(cancellationToken);

        if (lines.Count != journalEntryLineIds.Count)
            return Result.Failure<LetteringValidation>(Error.Validation("JournalEntryLine", "Une ou plusieurs lignes sont introuvables."));

        if (lines.Any(l => !string.IsNullOrEmpty(l.LetteringCode)))
            return Result.Failure<LetteringValidation>(Error.Validation("Lettering", "Une ou plusieurs lignes sont déjà lettrées."));

        var account = lines[0].AccountNumber;
        if (lines.Any(l => l.AccountNumber != account))
            return Result.Failure<LetteringValidation>(Error.Validation("AccountNumber", "Toutes les lignes doivent être sur le même compte."));

        var debit = lines.Sum(l => l.DebitAmount.Amount);
        var credit = lines.Sum(l => l.CreditAmount.Amount);
        var balanced = Math.Round(debit, 3) == Math.Round(credit, 3);
        if (!balanced && !allowPartial)
        {
            return Result.Failure<LetteringValidation>(Error.Validation("Balance",
                "Le lettrage nécessite une égalité débit / crédit (ou cochez « lettrage partiel »)."));
        }

        var isPartial = !balanced;
        var currency = lines[0].DebitAmount.Currency;
        if (lines.Any(l => l.DebitAmount.Currency != currency || l.CreditAmount.Currency != currency))
            return Result.Failure<LetteringValidation>(Error.Validation("Currency", "Les lignes doivent partager la même devise."));

        return Result.Success(new LetteringValidation(lines, account, isPartial, currency, debit, credit));
    }

    private sealed record LetteringValidation(
        List<JournalEntryLine> Lines,
        string Account,
        bool IsPartial,
        string Currency,
        decimal Debit,
        decimal Credit);

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

        ctx.LetteringGroups.Remove(group);
        await ctx.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Prochain code de lettrage : max de la partie numérique des codes existants + 1 (préfixe
    /// « L » définitif, « P » partiel). Inclut les groupes ajoutés dans le contexte courant
    /// mais pas encore persistés (lettrage batch dans une transaction ambiante).
    /// </summary>
    private static async Task<string> NextCodeAsync(TenantDbContext ctx, bool isPartial, CancellationToken cancellationToken)
    {
        var codes = await ctx.LetteringGroups
            .AsNoTracking()
            .Select(g => g.Code)
            .ToListAsync(cancellationToken);

        var pendingCodes = ctx.ChangeTracker.Entries<LetteringGroup>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.Code);

        var next = codes.Concat(pendingCodes)
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

        var paymentEntry = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.SourceEntityType == sourceEntityType && j.SourceEntityId == sourceEntityId, cancellationToken);

        if (paymentEntry is null)
            return Result.Success();

        var invoiceEntry = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.SourceEntityType == invoiceSourceType && j.SourceEntityId == invoiceSourceId, cancellationToken);

        if (invoiceEntry is null)
            return Result.Success();

        var invoiceThirdPartyLines = invoiceEntry.Lines
            .Where(l => l.ThirdPartyId.HasValue && string.IsNullOrEmpty(l.LetteringCode))
            .ToList();

        var paymentThirdPartyLines = paymentEntry.Lines
            .Where(l => l.ThirdPartyId.HasValue && string.IsNullOrEmpty(l.LetteringCode))
            .ToList();

        foreach (var invoiceLine in invoiceThirdPartyLines)
        {
            var matchingPaymentLine = paymentThirdPartyLines
                .FirstOrDefault(p => p.AccountNumber == invoiceLine.AccountNumber);

            if (matchingPaymentLine is null)
                continue;

            var totalDebit = invoiceLine.DebitAmount.Amount + matchingPaymentLine.DebitAmount.Amount;
            var totalCredit = invoiceLine.CreditAmount.Amount + matchingPaymentLine.CreditAmount.Amount;

            if (Math.Round(totalDebit, 3) != Math.Round(totalCredit, 3))
                continue;

            var lineIds = new List<Guid> { invoiceLine.Id, matchingPaymentLine.Id };
            var result = await ManualLetterAsync(lineIds, allowPartial: false, cancellationToken);
            if (result.IsFailure)
                continue;
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

        var letterRequests = new List<(IReadOnlyList<Guid> LineIds, bool AllowPartial)>();

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

            letterRequests.Add((new List<Guid> { creditLine.Id, debitLine.Id }, !balanced));
        }

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
                letterRequests.Add((lineIds, !balanced));
            }
        }

        if (letterRequests.Count == 0)
            return Result.Success();

        if (_ambient.IsActive)
        {
            await using var letterCtx = _contextFactory.CreateContext();
            foreach (var (lineIds, allowPartial) in letterRequests)
            {
                var result = await LetterInContextAsync(
                    letterCtx, lineIds, allowPartial, saveChanges: false, cancellationToken);
                if (result.IsFailure)
                    return result;
            }

            await letterCtx.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        foreach (var (lineIds, allowPartial) in letterRequests)
        {
            var result = await ManualLetterAsync(lineIds, allowPartial, cancellationToken);
            if (result.IsFailure)
                return result;
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
