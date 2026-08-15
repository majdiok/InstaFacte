using System.Diagnostics;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Treasury;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Treasury;

/// <summary>
/// Assemble la projection de trésorerie : solde d'ouverture comptable, flux collectés par source,
/// agrégats mensuels, scénarios et alertes.
/// </summary>
/// <remarks>
/// Le run et ses quatre tables enfants sont écrits dans une seule unité de travail : un run sans
/// ses buckets ni ses scénarios afficherait un écran vide en prétendant avoir abouti.
/// </remarks>
public sealed class CashFlowForecastService : ICashFlowForecastService
{
    private readonly ITreasuryPositionService _position;
    private readonly IEnumerable<ICashFlowSourceCollector> _collectors;
    private readonly ICashFlowForecastRepository _repository;
    private readonly ICashFlowForecastSettingsRepository _settingsRepository;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly TreasuryForecastOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CashFlowForecastService> _logger;
    private readonly ICashFlowAiAdvisor? _aiAdvisor;

    /// <param name="aiAdvisor">
    /// Optionnel : absent quand la couche IA est éteinte. Paramètre facultatif, comme les services
    /// optionnels du module Prévisions IA, pour que le service reste instanciable en test sans
    /// aucun fournisseur de modèle.
    /// </param>
    public CashFlowForecastService(
        ITreasuryPositionService position,
        IEnumerable<ICashFlowSourceCollector> collectors,
        ICashFlowForecastRepository repository,
        ICashFlowForecastSettingsRepository settingsRepository,
        ITenantUnitOfWork unitOfWork,
        IOptions<TreasuryForecastOptions> options,
        TimeProvider timeProvider,
        ILogger<CashFlowForecastService> logger,
        ICashFlowAiAdvisor? aiAdvisor = null)
    {
        _position = position;
        _collectors = collectors;
        _repository = repository;
        _settingsRepository = settingsRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _aiAdvisor = aiAdvisor;
    }

    public async Task<Result<CashFlowForecastRun>> GetOrComputeAsync(
        int horizonMonths,
        CancellationToken cancellationToken = default)
    {
        var horizon = NormalizeHorizon(horizonMonths);
        var existing = await _repository.GetLatestComputedAsync(horizon, cancellationToken);

        if (existing is not null)
        {
            var age = _timeProvider.GetUtcNow().UtcDateTime - existing.ComputedAt;
            if (age < TimeSpan.FromMinutes(Math.Max(1, _options.CacheTtlMinutes)))
                return Result.Success(existing);
        }

        return await RecomputeAsync(horizon, userId: null, cancellationToken);
    }

    public async Task<Result<CashFlowForecastRun>> RecomputeAsync(
        int horizonMonths,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var horizon = NormalizeHorizon(horizonMonths);
        var stopwatch = Stopwatch.StartNew();

        var today = _timeProvider.GetLocalNow().DateTime.Date;
        var from = today;
        var to = new DateTime(today.Year, today.Month, 1).AddMonths(horizon).AddDays(-1);

        var settings = await _settingsRepository.GetAsync(cancellationToken);
        var payDay = settings?.PayrollPaymentDayOfMonth ?? _options.PayrollPaymentDayOfMonth;

        var context = new CashFlowCollectionContext
        {
            From = from,
            To = to,
            Today = today,
            Options = _options,
            PayrollPaymentDayOfMonth = payDay
        };

        var position = await _position.GetCashPositionAsync(from, cancellationToken);

        var (lines, countsBySource) = await CollectAsync(context, cancellationToken);

        var deterministic = CashFlowScenarioEngine.Compute(
            lines,
            position.TotalBalance,
            from,
            to,
            _options.ConfidenceZ);

        var run = CashFlowForecastRun.Start(from, to, horizon, position.TotalBalance, position.Currency, userId);

        AttachBuckets(run, deterministic, position.TotalBalance);
        AttachLines(run, lines);
        AttachScenarios(run, deterministic);

        foreach (var insight in CashFlowAlertRules.Build(deterministic.Monthly, lines, position.TotalBalance, settings))
            run.AddInsight(insight);

        await ApplyAiAnalysisAsync(run, cancellationToken);

        run.Complete(
            deterministic.TotalInflows,
            deterministic.TotalOutflows,
            deterministic.ConfidencePercent,
            ForecastMethod.Sma,
            (int)stopwatch.ElapsedMilliseconds,
            BuildInputsJson(context, position, countsBySource));

        var persisted = await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _repository.AddAsync(run, ct);
            await _repository.DeletePreviousRunsAsync(horizon, run.Id, ct);
            return Result.Success(run);
        }, cancellationToken);

        if (persisted.IsFailure)
        {
            _logger.LogError(
                "Prévisionnel de trésorerie : échec de persistance du run {RunId} ({Error}).",
                run.Id,
                persisted.Error);
            return persisted;
        }

        _logger.LogInformation(
            "Prévisionnel de trésorerie recalculé : horizon {Horizon} mois, {LineCount} flux, {DurationMs} ms.",
            horizon,
            lines.Count,
            stopwatch.ElapsedMilliseconds);

        return Result.Success(run);
    }

    /// <summary>
    /// Soumet la projection déterministe au modèle, puis applique son analyse sous garde-fous.
    /// </summary>
    /// <remarks>
    /// Jamais bloquant : l'advisor renvoie <c>null</c> à la moindre défaillance, et le bloc est de
    /// toute façon protégé. Un modèle indisponible ne doit pas priver l'utilisateur de sa
    /// projection — il le prive seulement de son commentaire.
    /// </remarks>
    private async Task ApplyAiAnalysisAsync(CashFlowForecastRun run, CancellationToken cancellationToken)
    {
        if (_aiAdvisor is null || !_options.Ai.Enabled) return;

        try
        {
            var analysis = await _aiAdvisor.AnalyzeAsync(run, cancellationToken);
            if (analysis is null) return;

            var adjusted = CashFlowAiAdjustmentApplier.Apply(run, analysis, _options.Ai);
            run.MarkAiAnalyzed(_options.Ai.ModelOverride ?? "default", adjusted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Trésorerie prévisionnelle : l'analyse IA a échoué, la projection déterministe est conservée.");
        }
    }

    /// <summary>
    /// Exécute les collecteurs actifs, séquentiellement.
    /// </summary>
    /// <remarks>
    /// L'échec d'une source ne fait pas tomber la projection : mieux vaut une prévision amputée
    /// d'une source, signalée dans <c>InputsJson</c>, qu'un écran en erreur. Les autres sources
    /// restent exploitables.
    /// </remarks>
    private async Task<(List<CashFlowLineDraft> Lines, Dictionary<string, int> Counts)> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken)
    {
        var lines = new List<CashFlowLineDraft>();
        var counts = new Dictionary<string, int>();

        foreach (var collector in _collectors)
        {
            if (!collector.IsEnabled(_options))
            {
                counts[collector.SourceType.ToString()] = -1;
                continue;
            }

            try
            {
                var collected = await collector.CollectAsync(context, cancellationToken);
                lines.AddRange(collected);
                counts[collector.SourceType.ToString()] = collected.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Prévisionnel de trésorerie : la source {Source} a échoué et sera absente de la projection.",
                    collector.SourceType);
                counts[collector.SourceType.ToString()] = -2;
            }
        }

        return (lines, counts);
    }

    private static void AttachBuckets(
        CashFlowForecastRun run,
        CashFlowDeterministicResult deterministic,
        decimal openingBalance)
    {
        var opening = openingBalance;

        foreach (var month in deterministic.Monthly.OrderBy(m => m.SequenceIndex))
        {
            var halfWidth = CashFlowScenarioEngine.IntervalHalfWidth(
                deterministic.Monthly,
                month.SequenceIndex,
                1.96);

            var bucket = CashFlowForecastBucket.Create(
                month.SequenceIndex,
                month.PeriodStart,
                month.PeriodEnd,
                opening,
                month.Inflows,
                month.Outflows,
                halfWidth);

            run.AddBucket(bucket);
            opening = bucket.ClosingBalance;
        }
    }

    private static void AttachLines(CashFlowForecastRun run, IReadOnlyList<CashFlowLineDraft> lines)
    {
        foreach (var draft in lines)
        {
            run.AddLine(CashFlowForecastLine.Create(
                draft.Direction,
                draft.SourceType,
                draft.Label,
                draft.ContractualDate,
                draft.ExpectedDate,
                draft.Amount,
                draft.ProbabilityPercent,
                draft.IsConfirmed,
                draft.SourceId,
                draft.SourceReference,
                draft.ThirdPartyName));
        }
    }

    private static void AttachScenarios(CashFlowForecastRun run, CashFlowDeterministicResult deterministic)
    {
        foreach (var scenario in deterministic.Scenarios)
        {
            run.AddScenario(CashFlowScenario.Create(
                scenario.Kind,
                scenario.ClosingBalance,
                scenario.NetFlow,
                scenario.ProbabilityPercent,
                scenario.AssumptionsJson));
        }
    }

    /// <summary>
    /// Empreinte des paramètres et du volume collecté, pour pouvoir rejouer et auditer un run.
    /// Un compte à −1 signale une source désactivée, −2 une source en échec.
    /// </summary>
    private string BuildInputsJson(
        CashFlowCollectionContext context,
        CashPositionDto position,
        IReadOnlyDictionary<string, int> countsBySource)
    {
        var payload = new
        {
            from = context.From.ToString("yyyy-MM-dd"),
            to = context.To.ToString("yyyy-MM-dd"),
            today = context.Today.ToString("yyyy-MM-dd"),
            openingBalance = position.TotalBalance,
            openingAccounts = position.Accounts.Count,
            payrollPaymentDayOfMonth = context.PayrollPaymentDayOfMonth,
            confidenceZ = _options.ConfidenceZ,
            maxPaymentDelayShiftDays = _options.MaxPaymentDelayShiftDays,
            receivableProbability = new
            {
                notDue = _options.ReceivableProbability.NotDuePercent,
                upTo30 = _options.ReceivableProbability.OverdueUpTo30Percent,
                d31To60 = _options.ReceivableProbability.Overdue31To60Percent,
                d61To90 = _options.ReceivableProbability.Overdue61To90Percent,
                over90 = _options.ReceivableProbability.OverdueOver90Percent
            },
            countsBySource
        };

        var json = JsonSerializer.Serialize(payload);
        return json.Length <= 8000 ? json : json[..8000];
    }

    private int NormalizeHorizon(int horizonMonths)
    {
        if (horizonMonths <= 0) return _options.DefaultHorizonMonths;
        return Math.Clamp(horizonMonths, 1, Math.Max(1, _options.MaxHorizonMonths));
    }
}
