namespace FactuTrust.Application.Configuration;

/// <summary>
/// Configuration options for the AI Forecasting module (sales revenue forecasting,
/// stock replenishment recommendations, promotion suggestions, ABC/XYZ classification).
/// Bound from the "Forecasting" section in appsettings.json.
/// </summary>
public sealed class ForecastingOptions
{
    public const string SectionName = "Forecasting";

    /// <summary>
    /// Master feature flag. When false, no forecasting service is registered, no IA tool is exposed,
    /// no migration logic for forecasting tables is enforced, and no background job runs.
    /// Default false to guarantee zero impact on existing tenants until activation is explicit.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Tenant-wide default supplier lead time in days, used by replenishment math
    /// (ROP = d × L + SS). Override per product is a Phase 2 enhancement.
    /// </summary>
    public int DefaultLeadTimeDays { get; set; } = 7;

    /// <summary>Horizon in days used when no horizon is specified explicitly by the caller.</summary>
    public int DefaultHorizonDays { get; set; } = 30;

    /// <summary>Minimum number of complete months required to use Holt-Winters multiplicative.</summary>
    public int MinHistoryMonthsForHoltWinters { get; set; } = 24;

    /// <summary>Minimum number of historical data points required to use the Holt double-exponential smoothing.</summary>
    public int MinHistoryPointsForHolt { get; set; } = 6;

    /// <summary>Z-score for the prediction interval (1.96 → 95%, 1.65 → 90%).</summary>
    public double ConfidenceZ { get; set; } = 1.96;

    /// <summary>TTL in minutes for IMemoryCache entries scoped per tenant.</summary>
    public int CacheTtlMinutes { get; set; } = 60;

    /// <summary>Hour (Africa/Tunis) at which the daily recompute background job triggers.</summary>
    public int RecomputeHourTunis { get; set; } = 2;

    /// <summary>Day of week when the promotion-window detector scans the next 30 days. ISO name (Monday, Tuesday, …).</summary>
    public string PromotionScanDayOfWeek { get; set; } = "Monday";

    /// <summary>Hour (Africa/Tunis) at which the weekly promotion detector triggers.</summary>
    public int PromotionScanHourTunis { get; set; } = 3;

    /// <summary>When false, the daily recompute hosted service is not started even if Enabled is true.</summary>
    public bool BackgroundRecomputeEnabled { get; set; }

    /// <summary>When false, the promotion-window detector hosted service is not started.</summary>
    public bool PromotionDetectorEnabled { get; set; }

    /// <summary>Cumulative revenue % threshold to assign ABC class A (≤ this value = A).</summary>
    public double AbcAClassThreshold { get; set; } = 80.0;

    /// <summary>Cumulative revenue % threshold separating B from C (≤ this value = B, &gt; = C).</summary>
    public double AbcBClassThreshold { get; set; } = 95.0;

    /// <summary>Demand coefficient of variation threshold for class X (&lt; this value = X).</summary>
    public double XyzXClassThreshold { get; set; } = 0.5;

    /// <summary>Demand coefficient of variation threshold for class Y (&lt; this value = Y, ≥ = Z).</summary>
    public double XyzYClassThreshold { get; set; } = 1.0;

    /// <summary>Number of days without sales after which a product with stock &gt; 0 becomes a destockage candidate.</summary>
    public int DormantProductDays { get; set; } = 90;

    /// <summary>Stock-on-hand multiplier of MaximumStock above which a product is flagged as overstock.</summary>
    public double OverstockMultiplier { get; set; } = 1.2;

    /// <summary>Throttle: maximum manual /recompute runs per tenant per 24h.</summary>
    public int MaxRecomputeRunsPerDay { get; set; } = 2;

    /// <summary>
    /// Default seasonal uplift factors per product-category × commercial-event.
    /// Outer key = category name (canonical, accent-insensitive lookup at runtime),
    /// inner key = TunisianEventCode (e.g. Ramadan, AidAlFitr, SoldesEte, RentreeScolaire),
    /// value = multiplicative uplift (1.0 = no effect, 1.6 = +60% expected demand).
    /// Tenant overrides come in Phase 2.
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> SeasonalFactors { get; set; } = new();

    /// <summary>
    /// Replenishment V2 — feature-flagged successor of the V1 replenishment pipeline.
    /// Configuration of the single Replenishment pipeline (the legacy V1 was removed on 2026-05-13).
    /// </summary>
    public ReplenishmentOptions Replenishment { get; set; } = new();
}

/// <summary>
/// Configuration for the Replenishment module. The `Enabled` switch was removed in the
/// 2026-05-13 cutover — gating is now done by the master <see cref="ForecastingOptions.Enabled"/> flag only.
/// </summary>
public sealed class ReplenishmentOptions
{
    /// <summary>
    /// When true, "Préparer le bon de commande" actually creates draft <c>PurchaseOrder</c> entities
    /// grouped by supplier and links each recommendation via <c>LinkedPurchaseOrderId</c> (fix F-C1, F-C2).
    /// </summary>
    public bool AutoCreatePurchaseOrders { get; set; } = true;

    /// <summary>
    /// When true, the V2 board enforces a Manager → Buyer → Budget workflow before PO creation.
    /// </summary>
    public bool MultiLevelApproval { get; set; }

    /// <summary>When true, the V2 board polls for fresh recommendations and surfaces a "new recos" badge.</summary>
    public bool RealTimeNotifications { get; set; } = true;

    /// <summary>When true, the KPI bar (service rate, stock-out rate, top-N urgencies) is computed and shown.</summary>
    public bool KpiDashboard { get; set; } = true;

    /// <summary>
    /// Threshold (days) below which a recommendation is flagged as "Urgent" in the UI
    /// (when <c>DaysOfStockRemaining &lt; UrgencyThresholdDays</c>). Default 3.
    /// </summary>
    public int UrgencyThresholdDays { get; set; } = 3;

    /// <summary>
    /// Default service-level Z-score (1.65 = 95%, 1.96 = 97.5%, 2.33 = 99%). V2 may override
    /// per ABC class in a future revision; V1 hardcoded 1.65 (fix F-M15).
    /// </summary>
    public decimal DefaultServiceLevelZ { get; set; } = 1.65m;

    /// <summary>
    /// Maximum number of recommendations that can be turned into draft POs in a single "Prepare" call.
    /// Mirrors the existing V1 cap to avoid runaway batches.
    /// </summary>
    public int MaxPrepareBatchSize { get; set; } = 200;

    /// <summary>
    /// Window (hours) during which a user can undo an Approve/Dismiss decision via
    /// <c>RevertToPending</c>. Beyond this window the action is final.
    /// </summary>
    public int UndoWindowHours { get; set; } = 24;
}
