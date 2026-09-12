using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Services.Accounting;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.Commands;

/// <summary>
/// Réévalue les positions en devise à la clôture d'une période, et contre-passe l'écriture à
/// l'ouverture de la suivante.
///
/// <para>
/// <b>Idempotent</b> : l'écriture est rattachée à la source <c>ClosingRevaluation</c> avec un
/// identifiant déterministe dérivé de l'exercice et du mois. Rejouer le traitement sur la même
/// période est refusé plutôt que de produire un doublon.
/// </para>
///
/// <para>
/// <b>Contre-passation obligatoire.</b> Les écarts de conversion sont des comptes de
/// régularisation, pas des comptes de résultat : sans contre-passation à l'ouverture suivante, la
/// réévaluation du mois d'après se cumulerait à celle-ci.
/// </para>
///
/// <para>
/// ⚠️ Ce traitement touche les états financiers. Une relecture par un comptable est requise avant
/// activation en production.
/// </para>
/// </summary>
public sealed record RunClosingRevaluationCommand(RunClosingRevaluationRequest Request)
    : IRequest<Result<ClosingRevaluationResultDto>>;

public sealed class RunClosingRevaluationCommandHandler
    : IRequestHandler<RunClosingRevaluationCommand, Result<ClosingRevaluationResultDto>>
{
    private const string SourceType = "ClosingRevaluation";
    private const string JournalCode = "JOD";
    private const string Label = "ECART DE CONVERSION";
    private const string ReversalLabel = "EXTOURNE ECART DE CONVERSION";
    private const string ProvisionLabel = "PROVISION POUR PERTES DE CHANGE";

    private readonly IJournalEntryRepository _journalEntries;
    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAccountingPeriodService _periodService;
    private readonly IExchangeRateResolver _exchangeRates;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public RunClosingRevaluationCommandHandler(
        IJournalEntryRepository journalEntries,
        IChartOfAccountRepository chartOfAccounts,
        IAccountingPeriodService periodService,
        IExchangeRateResolver exchangeRates,
        ITenantUnitOfWork unitOfWork,
        IAuditService auditService,
        ICurrentUser currentUser,
        IOptions<AccountingSettings> settings)
    {
        _journalEntries = journalEntries;
        _chartOfAccounts = chartOfAccounts;
        _periodService = periodService;
        _exchangeRates = exchangeRates;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<ClosingRevaluationResultDto>> Handle(
        RunClosingRevaluationCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.MultiCurrencyEnabled)
            return Fail("La gestion multi-devises n'est pas activée.");

        var r = request.Request;
        if (r.Month is < 1 or > 12)
            return Fail("Le mois doit être compris entre 1 et 12.");

        var periodEnd = new DateTime(r.FiscalYear, r.Month, 1).AddMonths(1).AddDays(-1);
        var nextPeriodStart = periodEnd.AddDays(1);

        // Idempotence : refus explicite plutôt qu'un second jeu d'écritures.
        var sourceId = DeterministicSourceId(r.FiscalYear, r.Month);
        var existing = await _journalEntries.GetActiveBySourceAsync(SourceType, sourceId, cancellationToken);
        if (existing is not null)
            return Fail($"La réévaluation de {r.Month:00}/{r.FiscalYear} a déjà été effectuée (écriture n° {existing.EntryNumber}).");

        var accounts = await ValidateAccountsAsync(r, cancellationToken);
        if (accounts.IsFailure)
            return Result.Failure<ClosingRevaluationResultDto>(accounts.Error);

        // Positions ouvertes, puis réévaluation au taux de clôture de chaque devise.
        var positions = await _journalEntries.GetOpenForeignCurrencyPositionsAsync(periodEnd, cancellationToken);
        if (positions.Count == 0)
            return Fail("Aucune position en devise ouverte à cette date : rien à réévaluer.");

        var revalued = new List<RevaluedPosition>();
        foreach (var p in positions)
        {
            var rate = await _exchangeRates.ResolveAsync(p.CurrencyCode, periodEnd, null, cancellationToken);
            if (rate.IsFailure)
                return Result.Failure<ClosingRevaluationResultDto>(rate.Error);

            var position = new CurrencyPosition(p.AccountNumber, p.CurrencyCode, p.NetInCurrency, p.NetFunctional);
            var result = ClosingRevaluation.Revalue(position, rate.Value.Rate);
            if (result.IsFailure)
                return Result.Failure<ClosingRevaluationResultDto>(result.Error);

            revalued.Add(result.Value);
        }

        var lines = ClosingRevaluation.BuildLines(revalued, r.GainAccount.Trim(), r.LossAccount.Trim(), Label);
        if (lines.Count == 0)
            return Fail("Aucun écart de conversion à cette date : les positions sont déjà au taux de clôture.");

        var provisionLines = HasProvision(r)
            ? ClosingRevaluation.BuildProvisionLines(revalued, r.ProvisionExpenseAccount!.Trim(), r.ProvisionAccount!.Trim(), ProvisionLabel)
            : Array.Empty<JournalLineInput>();

        Guid revaluationId = Guid.Empty, reversalId = Guid.Empty, provisionId = Guid.Empty;

        var outcome = await _unitOfWork.ExecuteAsync(async ct =>
        {
            var period = await _periodService.EnsureOpenPeriodAsync(periodEnd, ct);
            if (period.IsFailure)
                return Result.Failure(period.Error);

            var nextPeriod = await _periodService.EnsureOpenPeriodAsync(nextPeriodStart, ct);
            if (nextPeriod.IsFailure)
                return Result.Failure(nextPeriod.Error);

            var create = await AddEntryAsync(lines, periodEnd, period.Value.Id, Label, sourceId, ct);
            if (create.IsFailure)
                return Result.Failure(create.Error);
            revaluationId = create.Value;

            // Contre-passation : mêmes lignes, sens inversé, datée du premier jour suivant.
            var reversalLines = lines
                .Select(l => l with { Debit = l.Credit, Credit = l.Debit })
                .ToList();

            var reverse = await AddEntryAsync(reversalLines, nextPeriodStart, nextPeriod.Value.Id, ReversalLabel, null, ct);
            if (reverse.IsFailure)
                return Result.Failure(reverse.Error);
            reversalId = reverse.Value;

            if (provisionLines.Count > 0)
            {
                var provision = await AddEntryAsync(provisionLines, periodEnd, period.Value.Id, ProvisionLabel, null, ct);
                if (provision.IsFailure)
                    return Result.Failure(provision.Error);
                provisionId = provision.Value;
            }

            return Result.Success();
        }, cancellationToken);

        if (outcome.IsFailure)
            return Result.Failure<ClosingRevaluationResultDto>(outcome.Error);

        var dto = new ClosingRevaluationResultDto
        {
            RevaluationEntryId = revaluationId,
            ReversalEntryId = reversalId,
            ProvisionEntryId = provisionId == Guid.Empty ? null : provisionId,
            PositionCount = revalued.Count,
            TotalLatentGain = revalued.Where(p => p.IsLatentGain).Sum(p => p.Delta),
            TotalLatentLoss = revalued.Where(p => p.IsLatentLoss).Sum(p => Math.Abs(p.Delta))
        };

        await _auditService.LogAsync(
            AuditActions.Accounting.ClosingRevaluationRun,
            "JournalEntry",
            revaluationId,
            newValues: new { r.FiscalYear, r.Month, dto.PositionCount, dto.TotalLatentGain, dto.TotalLatentLoss },
            cancellationToken: cancellationToken);

        return Result.Success(dto);
    }

    private async Task<Result<Guid>> AddEntryAsync(
        IReadOnlyList<JournalLineInput> lines,
        DateTime date,
        Guid periodId,
        string label,
        Guid? sourceId,
        CancellationToken cancellationToken)
    {
        var number = await _journalEntries.ReserveNextEntryNumberAsync(JournalCode, date.Year, cancellationToken);
        var create = JournalEntry.Create(
            number, JournalCode, date, label, periodId,
            isAutoGenerated: true,
            sourceEntityType: sourceId is null ? null : SourceType,
            sourceEntityId: sourceId,
            lines);

        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var entry = create.Value;
        entry.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _journalEntries.AddAsync(entry, cancellationToken);
        return Result.Success(entry.Id);
    }

    /// <summary>
    /// Les écarts de conversion sont des comptes de bilan (classes 1 et 2), la dotation une charge
    /// (classe 6) et la provision un compte de capitaux (classe 1). Sans ces bornes, l'utilisateur
    /// pourrait loger l'écart dans un compte de tiers et le faire disparaître du bilan.
    /// </summary>
    private async Task<Result> ValidateAccountsAsync(RunClosingRevaluationRequest r, CancellationToken cancellationToken)
    {
        var gain = await RequireAccountAsync(r.GainAccount, "12", "l'écart de conversion passif", cancellationToken);
        if (gain.IsFailure) return gain;

        var loss = await RequireAccountAsync(r.LossAccount, "12", "l'écart de conversion actif", cancellationToken);
        if (loss.IsFailure) return loss;

        if (string.Equals(r.GainAccount?.Trim(), r.LossAccount?.Trim(), StringComparison.Ordinal))
            return Result.Failure(Error.Validation("Accounts",
                "Les écarts de conversion actif et passif doivent utiliser deux comptes distincts."));

        if (!HasProvision(r))
            return Result.Success();

        var expense = await RequireAccountAsync(r.ProvisionExpenseAccount, "6", "la dotation aux provisions", cancellationToken);
        if (expense.IsFailure) return expense;

        return await RequireAccountAsync(r.ProvisionAccount, "1", "la provision pour pertes de change", cancellationToken);
    }

    private async Task<Result> RequireAccountAsync(
        string? accountNumber, string allowedClasses, string role, CancellationToken cancellationToken)
    {
        var number = (accountNumber ?? string.Empty).Trim();
        if (number.Length == 0)
            return Result.Failure(Error.Validation("AccountNumber", $"Le compte pour {role} est obligatoire."));

        var account = await _chartOfAccounts.GetByAccountNumberAsync(number, cancellationToken);
        if (account is null)
            return Result.Failure(Error.Validation("AccountNumber", $"Le compte {number} n'existe pas dans le plan comptable."));

        if (!account.IsActive)
            return Result.Failure(Error.Validation("AccountNumber", $"Le compte {number} est désactivé."));

        if (!allowedClasses.Contains(number[0]))
            return Result.Failure(Error.Validation("AccountNumber",
                $"Le compte pour {role} doit appartenir à la classe {string.Join(" ou ", allowedClasses.ToCharArray())}."));

        return Result.Success();
    }

    private static bool HasProvision(RunClosingRevaluationRequest r) =>
        !string.IsNullOrWhiteSpace(r.ProvisionExpenseAccount) && !string.IsNullOrWhiteSpace(r.ProvisionAccount);

    /// <summary>
    /// Identifiant de source déterministe : deux exécutions sur la même période visent la même
    /// source, ce qui rend le garde d'idempotence fiable sans table dédiée.
    /// </summary>
    private static Guid DeterministicSourceId(int fiscalYear, int month)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(fiscalYear).CopyTo(bytes, 0);
        BitConverter.GetBytes(month).CopyTo(bytes, 4);
        // Marqueur fixe : évite toute collision avec un autre usage de sourceEntityId.
        "REVAL".Select(c => (byte)c).ToArray().CopyTo(bytes, 8);
        return new Guid(bytes);
    }

    private static Result<ClosingRevaluationResultDto> Fail(string message) =>
        Result.Failure<ClosingRevaluationResultDto>(Error.Validation("ClosingRevaluation", message));
}
