namespace FactuTrust.Domain.Enums;

/// <summary>
/// Scope of a sales forecast — what the forecast applies to.
/// Used by the Forecasting module to identify the aggregation level.
/// </summary>
public enum ForecastScopeType
{
    /// <summary>Whole company / all products / all clients.</summary>
    Global = 1,

    /// <summary>One product category. ScopeId references ProductCategory.Id.</summary>
    Category = 2,

    /// <summary>One product. ScopeId references Product.Id.</summary>
    Product = 3,

    /// <summary>One warehouse. ScopeId references Warehouse.Id.</summary>
    Warehouse = 4,

    /// <summary>One client. ScopeId references Client.Id.</summary>
    Client = 5
}

/// <summary>
/// Time horizon for a sales forecast.
/// </summary>
public enum ForecastHorizon
{
    /// <summary>Next 7 days.</summary>
    Week = 1,

    /// <summary>Next 30 days.</summary>
    Month = 2,

    /// <summary>Next 90 days.</summary>
    Quarter = 3,

    /// <summary>Custom range (PeriodStart / PeriodEnd are explicit).</summary>
    Custom = 99
}

/// <summary>
/// Statistical method used to compute a forecast — stored on every forecast for auditability.
/// </summary>
public enum ForecastMethod
{
    /// <summary>Simple Moving Average (baseline).</summary>
    Sma = 1,

    /// <summary>Holt double-exponential smoothing (level + trend).</summary>
    Holt = 2,

    /// <summary>Holt-Winters multiplicative (level + trend + seasonality 12).</summary>
    HoltWinters = 3,

    /// <summary>Fallback: SMA + Tunisian-calendar seasonal heuristic, used when history is too short.</summary>
    CalendarHeuristic = 4
}

/// <summary>
/// Workflow status of a replenishment recommendation produced by the forecasting engine.
/// </summary>
public enum ReplenishmentStatus
{
    /// <summary>Generated, awaiting user decision.</summary>
    Pending = 1,

    /// <summary>User approved — a draft purchase order can/has been prepared.</summary>
    Approved = 2,

    /// <summary>User dismissed the recommendation (with optional reason).</summary>
    Dismissed = 3,

    /// <summary>A purchase order has been created and linked.</summary>
    Ordered = 4,

    /// <summary>Stale — superseded by a newer recommendation.</summary>
    Superseded = 5
}

/// <summary>
/// Type of a promotion recommendation, indicating the underlying business rationale.
/// </summary>
public enum PromotionRecommendationType
{
    /// <summary>Destock a dormant or slow-moving article.</summary>
    Destockage = 1,

    /// <summary>Reduce overstock (stock-on-hand &gt; max × multiplier).</summary>
    Surstock = 2,

    /// <summary>Cross-sell a low-rotation article via a bundle.</summary>
    CrossSell = 3,

    /// <summary>Pre-peak push: highlight a star product before a known seasonal peak (no discount).</summary>
    PrePic = 4,

    /// <summary>Calendar-driven seasonal promotion (Ramadan, Aïd, Soldes, Rentrée scolaire…).</summary>
    Saisonnier = 5,

    /// <summary>Margin push on classes A* (high CA, low variability) — e.g. cash-back, bundle.</summary>
    MargePush = 6
}

/// <summary>
/// Workflow status of a promotion recommendation.
/// </summary>
public enum PromotionRecommendationStatus
{
    /// <summary>Generated, awaiting user decision.</summary>
    Pending = 1,

    /// <summary>User accepted (draft discount can/has been prepared in the catalog).</summary>
    Accepted = 2,

    /// <summary>User dismissed the recommendation.</summary>
    Dismissed = 3,

    /// <summary>An actual discount/promotion has been activated and is linked.</summary>
    Activated = 4,

    /// <summary>Validity window has passed without activation.</summary>
    Expired = 5
}

/// <summary>
/// ABC class assigned to a product based on cumulative revenue contribution over a reference window.
/// </summary>
public enum AbcClass
{
    /// <summary>Top revenue contributors (≤ AbcAClassThreshold cumulative revenue %).</summary>
    A = 1,

    /// <summary>Mid contributors (between AbcAClassThreshold and AbcBClassThreshold).</summary>
    B = 2,

    /// <summary>Long tail (&gt; AbcBClassThreshold).</summary>
    C = 3,

    /// <summary>Unclassified (no sales in the reference window).</summary>
    Unclassified = 0
}

/// <summary>
/// XYZ class assigned to a product based on demand variability (coefficient of variation) over the reference window.
/// </summary>
public enum XyzClass
{
    /// <summary>Stable demand (CV &lt; XyzXClassThreshold).</summary>
    X = 1,

    /// <summary>Variable demand (XyzXClassThreshold ≤ CV &lt; XyzYClassThreshold).</summary>
    Y = 2,

    /// <summary>Erratic demand (CV ≥ XyzYClassThreshold).</summary>
    Z = 3,

    /// <summary>Unclassified (no sales in the reference window).</summary>
    Unclassified = 0
}

/// <summary>
/// Code identifying a Tunisian commercial event.
/// Encoded as string in DB to keep the calendar JSON-driven and forward-compatible.
/// </summary>
public static class TunisianEventCode
{
    // Religious / lunar (dates from embedded JSON table)
    public const string Ramadan = "Ramadan";
    public const string AidAlFitr = "AidAlFitr";
    public const string AidAlAdha = "AidAlAdha";
    public const string Mouled = "Mouled";
    public const string RasElAmHijri = "RasElAmHijri";

    // Civil holidays (fixed)
    public const string NouvelAn = "NouvelAn";                  // 1-jan
    public const string FeteRevolution = "FeteRevolution";      // 14-jan
    public const string FeteIndependance = "FeteIndependance";  // 20-mars
    public const string FeteMartyrs = "FeteMartyrs";            // 9-avr
    public const string FeteTravail = "FeteTravail";            // 1-mai
    public const string FeteRepublique = "FeteRepublique";      // 25-juil
    public const string FeteFemme = "FeteFemme";                // 13-août
    public const string FeteEvacuation = "FeteEvacuation";      // 15-oct

    // Commercial windows
    public const string SoldesHiver = "SoldesHiver";        // mi-janv → fin février
    public const string SoldesEte = "SoldesEte";            // mi-juillet → fin août
    public const string RentreeScolaire = "RentreeScolaire";// 15-août → 15-septembre
    public const string BlackFriday = "BlackFriday";        // dernier vendredi de novembre

    // Climatic seasons (broad uplifts)
    public const string Ete = "Ete";
    public const string Hiver = "Hiver";
}
