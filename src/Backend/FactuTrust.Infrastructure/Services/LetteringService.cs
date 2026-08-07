using System.Data;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactuTrust.Infrastructure.Services;

public sealed class LetteringService : ILetteringService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly TenantAmbientTransaction _ambient;
    private readonly ILogger<LetteringService> _logger;

    public LetteringService(
        ITenantDbContextFactory contextFactory,
        TenantAmbientTransaction ambient,
        ILogger<LetteringService>? logger = null)
    {
        _contextFactory = contextFactory;
        _ambient = ambient;
        _logger = logger ?? NullLogger<LetteringService>.Instance;
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

        // Écriture ACTIVE du cycle : une OD extournée (cycle rouvert puis revalidé) ne doit jamais
        // être choisie à la place de l'écriture courante.
        var payrollEntry = await ctx.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .Where(j => j.SourceEntityType == AccountingService.SourcePayrollRun
                        && j.SourceEntityId == payrollRunId
                        && !j.IsReversed)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (payrollEntry is null)
            return Result.Success();

        // Les groupes sont indexés par numéro de compte : un groupe de lettrage porte un compte
        // unique (invariant de LetteringGroup, vérifié par ValidateLinesAsync). Construire les
        // groupes à partir du compte rend cet invariant vrai par construction, et couvre
        // indifféremment l'OD ventilée par salarié (421xxxx) et l'OD agrégée (421) des cycles
        // validés avant les comptes auxiliaires.
        var runCredits = payrollEntry.Lines
            .Where(l => IsPersonnelPayable(l.AccountNumber)
                        && l.CreditAmount.Amount > 0
                        && string.IsNullOrEmpty(l.LetteringCode))
            .GroupBy(l => l.AccountNumber, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var paymentDebits = paymentEntry.Lines
            .Where(l => IsPersonnelPayable(l.AccountNumber)
                        && l.DebitAmount.Amount > 0
                        && string.IsNullOrEmpty(l.LetteringCode))
            .GroupBy(l => l.AccountNumber, StringComparer.Ordinal);

        var letterRequests = new List<(IReadOnlyList<Guid> LineIds, bool AllowPartial)>();

        foreach (var debitGroup in paymentDebits)
        {
            if (!runCredits.TryGetValue(debitGroup.Key, out var credits) || credits.Count == 0)
            {
                _logger.LogWarning(
                    "Lettrage paie : aucune contrepartie non lettrée sur le compte {Account} pour le "
                    + "cycle {PayrollRunId} (paiement {PayrollPaymentId}). Rapprochement à faire manuellement.",
                    debitGroup.Key, payrollRunId, payrollPaymentId);
                continue;
            }

            var group = credits.Concat(debitGroup).ToList();
            var debit = group.Sum(l => l.DebitAmount.Amount);
            var credit = group.Sum(l => l.CreditAmount.Amount);
            var balanced = Math.Round(debit, 3) == Math.Round(credit, 3);

            letterRequests.Add((group.Select(l => l.Id).ToList(), !balanced));
        }

        if (letterRequests.Count == 0)
            return Result.Success();

        // Best-effort, comme AutoLetterPaymentAsync : le rapprochement est un confort comptable,
        // il ne doit jamais faire échouer — donc annuler — un règlement déjà comptabilisé.
        if (_ambient.IsActive)
        {
            await using var letterCtx = _contextFactory.CreateContext();
            foreach (var (lineIds, allowPartial) in letterRequests)
            {
                // ValidateLinesAsync échoue avant toute mutation du contexte : passer au groupe
                // suivant laisse intactes les mutations déjà enregistrées, et le SaveChanges final
                // reste atteint.
                var result = await LetterInContextAsync(
                    letterCtx, lineIds, allowPartial, saveChanges: false, cancellationToken);
                if (result.IsFailure)
                    LogSkippedLettering(result, payrollPaymentId, payrollRunId);
            }

            await letterCtx.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        foreach (var (lineIds, allowPartial) in letterRequests)
        {
            var result = await ManualLetterAsync(lineIds, allowPartial, cancellationToken);
            if (result.IsFailure)
                LogSkippedLettering(result, payrollPaymentId, payrollRunId);
        }

        return Result.Success();
    }

    /// <summary>Vrai pour le compte de dettes envers le personnel (421) et ses auxiliaires 421xxxx.</summary>
    private static bool IsPersonnelPayable(string accountNumber) =>
        accountNumber.StartsWith(PayrollJournalEntryBuilder.PersonnelPayableAccount, StringComparison.Ordinal);

    private void LogSkippedLettering(Result result, Guid payrollPaymentId, Guid payrollRunId) =>
        _logger.LogWarning(
            "Lettrage paie non posé pour le paiement {PayrollPaymentId} (cycle {PayrollRunId}) : {Reason}. "
            + "Le paiement reste enregistré ; le rapprochement peut être fait manuellement.",
            payrollPaymentId, payrollRunId, result.Error.Description);

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
