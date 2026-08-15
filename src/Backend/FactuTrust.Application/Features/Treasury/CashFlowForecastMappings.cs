using FactuTrust.Application.Features.Treasury.Dtos;
using FactuTrust.Domain.Entities.Treasury;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Treasury;

/// <summary>
/// Conversion domaine → DTO du prévisionnel de trésorerie.
/// </summary>
/// <remarks>
/// Les énumérations sont sérialisées en <c>snake_case</c> et non par leur nom .NET : le front
/// travaille en unions de littéraux minuscules, et la sérialisation PascalCase par défaut du
/// backend a déjà causé des incohérences côté module Prévisions IA. Le point de conversion est
/// unique, ici.
/// </remarks>
public static class CashFlowForecastMappings
{
    /// <summary>Nombre d'encaissements mis en avant dans le bloc « Encaissements à venir ».</summary>
    public const int UpcomingInflowsCount = 5;

    public static CashFlowForecastDto ToDto(
        CashFlowForecastRun run,
        IReadOnlyList<CashFlowForecastLine> upcomingInflows,
        CashFlowForecastSettings? settings) =>
        new()
        {
            RunId = run.Id,
            PeriodStart = run.PeriodStart,
            PeriodEnd = run.PeriodEnd,
            HorizonMonths = run.HorizonMonths,
            Currency = run.Currency,
            OpeningBalance = run.OpeningBalance,
            ClosingBalance = run.ClosingBalance,
            TotalInflows = run.TotalInflows,
            TotalOutflows = run.TotalOutflows,
            NetFlow = run.NetFlow,
            ConfidencePercent = run.ConfidencePercent,
            ComputedAt = run.ComputedAt,
            DurationMs = run.DurationMs,
            AiAdjustmentApplied = run.AiAdjustmentApplied,
            AiModelRef = run.AiModelRef,
            Buckets = run.Buckets.OrderBy(b => b.SequenceIndex).Select(ToDto).ToList(),
            Scenarios = run.Scenarios.OrderBy(s => s.Kind).Select(ToDto).ToList(),
            Insights = run.Insights
                .OrderBy(i => i.Kind)
                .ThenBy(i => i.SortOrder)
                .Select(ToDto)
                .ToList(),
            UpcomingInflows = upcomingInflows.Select(ToDto).ToList(),
            Thresholds = ToDto(settings)
        };

    public static CashFlowBucketDto ToDto(CashFlowForecastBucket bucket) => new()
    {
        SequenceIndex = bucket.SequenceIndex,
        PeriodStart = bucket.PeriodStart,
        PeriodEnd = bucket.PeriodEnd,
        OpeningBalance = bucket.OpeningBalance,
        Inflows = bucket.Inflows,
        Outflows = bucket.Outflows,
        NetFlow = bucket.NetFlow,
        ClosingBalance = bucket.ClosingBalance,
        LowClosingBalance = bucket.LowClosingBalance,
        HighClosingBalance = bucket.HighClosingBalance
    };

    public static CashFlowScenarioDto ToDto(CashFlowScenario scenario) => new()
    {
        Kind = ToSlug(scenario.Kind),
        ClosingBalance = scenario.ClosingBalance,
        NetFlow = scenario.NetFlow,
        ProbabilityPercent = scenario.ProbabilityPercent,
        DeterministicProbabilityPercent = scenario.DeterministicProbabilityPercent,
        ProbabilitySource = scenario.ProbabilitySource == CashFlowProbabilitySource.AiAdjusted
            ? "ai_adjusted"
            : "deterministic",
        AiRationale = scenario.AiRationale
    };

    public static CashFlowInsightDto ToDto(CashFlowForecastInsight insight) => new()
    {
        Kind = insight.Kind switch
        {
            CashFlowInsightKind.Alert => "alert",
            CashFlowInsightKind.Driver => "driver",
            _ => "recommendation"
        },
        Severity = insight.Severity switch
        {
            CashFlowInsightSeverity.Critical => "critical",
            CashFlowInsightSeverity.Warning => "warning",
            _ => "info"
        },
        Origin = insight.Origin == CashFlowInsightOrigin.Ai ? "ai" : "rule",
        Impact = insight.Impact switch
        {
            CashFlowImpactLevel.High => "high",
            CashFlowImpactLevel.Medium => "medium",
            CashFlowImpactLevel.Low => "low",
            _ => null
        },
        ImpactDirection = ToSlug(insight.ImpactDirection),
        Title = insight.Title,
        Detail = insight.Detail,
        PeriodStart = insight.PeriodStart,
        EstimatedBalance = insight.EstimatedBalance
    };

    public static CashFlowLineDto ToDto(CashFlowForecastLine line) => new()
    {
        Id = line.Id,
        Direction = ToSlug(line.Direction)!,
        SourceType = ToSlug(line.SourceType),
        SourceId = line.SourceId,
        SourceReference = line.SourceReference,
        Label = line.Label,
        ThirdPartyName = line.ThirdPartyName,
        ContractualDate = line.ContractualDate,
        ExpectedDate = line.ExpectedDate,
        Amount = line.Amount,
        ProbabilityPercent = line.ProbabilityPercent,
        WeightedAmount = line.WeightedAmount,
        IsConfirmed = line.IsConfirmed
    };

    public static CashFlowThresholdsDto ToDto(CashFlowForecastSettings? settings) => new()
    {
        CriticalThreshold = settings?.CriticalThreshold ?? 0m,
        AlertThreshold = settings?.AlertThreshold ?? 0m,
        ComfortThreshold = settings?.ComfortThreshold ?? 0m,
        PayrollPaymentDayOfMonth = settings?.PayrollPaymentDayOfMonth
    };

    public static RecurringCashCommitmentDto ToDto(RecurringCashCommitment commitment) => new()
    {
        Id = commitment.Id,
        Label = commitment.Label,
        Direction = ToSlug(commitment.Direction)!,
        Amount = commitment.Amount,
        Currency = commitment.Currency,
        Frequency = ToSlug(commitment.Frequency),
        DayOfMonth = commitment.DayOfMonth,
        StartDate = commitment.StartDate,
        EndDate = commitment.EndDate,
        Category = commitment.Category,
        Notes = commitment.Notes,
        IsActive = commitment.IsActive
    };

    // ──────────────────── Slugs ────────────────────

    public static string? ToSlug(CashFlowDirection? direction) => direction switch
    {
        CashFlowDirection.Inflow => "inflow",
        CashFlowDirection.Outflow => "outflow",
        _ => null
    };

    public static string ToSlug(CashFlowScenarioKind kind) => kind switch
    {
        CashFlowScenarioKind.Optimistic => "optimistic",
        CashFlowScenarioKind.Realistic => "realistic",
        _ => "pessimistic"
    };

    public static string ToSlug(CashFlowSourceType source) => source switch
    {
        CashFlowSourceType.ClientInvoice => "client_invoice",
        CashFlowSourceType.ClientEffet => "client_effet",
        CashFlowSourceType.SalesOrderBacklog => "sales_order_backlog",
        CashFlowSourceType.SupplierInvoice => "supplier_invoice",
        CashFlowSourceType.SupplierEffet => "supplier_effet",
        CashFlowSourceType.PurchaseOrderCommitment => "purchase_order_commitment",
        CashFlowSourceType.Payroll => "payroll",
        CashFlowSourceType.PayrollContribution => "payroll_contribution",
        CashFlowSourceType.FiscalObligation => "fiscal_obligation",
        CashFlowSourceType.LoanInstallment => "loan_installment",
        CashFlowSourceType.RecurringCommitment => "recurring_commitment",
        CashFlowSourceType.RecurringJournalTemplate => "recurring_journal_template",
        _ => "manual"
    };

    public static string ToSlug(CashCommitmentFrequency frequency) => frequency switch
    {
        CashCommitmentFrequency.Weekly => "weekly",
        CashCommitmentFrequency.Monthly => "monthly",
        CashCommitmentFrequency.Quarterly => "quarterly",
        CashCommitmentFrequency.SemiAnnual => "semi_annual",
        _ => "annual"
    };

    // ──────────────────── Parsing des requêtes ────────────────────

    public static CashFlowDirection ParseDirection(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "inflow" => CashFlowDirection.Inflow,
        "outflow" => CashFlowDirection.Outflow,
        _ => throw new ArgumentException("Le sens doit valoir « inflow » ou « outflow ».", nameof(value))
    };

    public static CashCommitmentFrequency ParseFrequency(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "weekly" => CashCommitmentFrequency.Weekly,
        "monthly" => CashCommitmentFrequency.Monthly,
        "quarterly" => CashCommitmentFrequency.Quarterly,
        "semi_annual" => CashCommitmentFrequency.SemiAnnual,
        "annual" => CashCommitmentFrequency.Annual,
        _ => throw new ArgumentException(
            "La périodicité doit valoir weekly, monthly, quarterly, semi_annual ou annual.",
            nameof(value))
    };

    public static CashFlowSourceType? ParseSourceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var slug = value.Trim().ToLowerInvariant();
        foreach (var candidate in Enum.GetValues<CashFlowSourceType>())
        {
            if (ToSlug(candidate) == slug) return candidate;
        }

        throw new ArgumentException($"Source de flux inconnue : « {value} ».", nameof(value));
    }

    public static CashFlowDirection? ParseOptionalDirection(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDirection(value);
}
