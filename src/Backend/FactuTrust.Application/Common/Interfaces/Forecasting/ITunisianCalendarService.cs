namespace FactuTrust.Application.Common.Interfaces.Forecasting;

/// <summary>
/// One commercial / civil event in the Tunisian calendar (Ramadan window, Aïd, Soldes, fêtes nationales…).
/// </summary>
public sealed record TunisianEvent
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public bool IsHoliday { get; init; }
    public bool IsCommercialWindow { get; init; }

    /// <summary>Human-readable category label (e.g. "ReligieuseLunaire", "FerieCivil", "SoldesOfficielles").</summary>
    public required string Category { get; init; }
}

/// <summary>
/// Read-only, deterministic Tunisian commercial calendar.
/// Fixed civil holidays are computed; lunar holidays (Ramadan, Aïd, Mouled, Ras El Am Hijri) are sourced
/// from an embedded JSON table covering 2024–2030. Tenant overrides are out of scope for V1.
/// </summary>
public interface ITunisianCalendarService
{
    /// <summary>Return all events that overlap the inclusive range [from..to].</summary>
    IReadOnlyList<TunisianEvent> GetEvents(DateTime from, DateTime to);

    /// <summary>True if the given date is a public holiday (civil or religious).</summary>
    bool IsHoliday(DateTime date);

    /// <summary>
    /// Return the multiplicative seasonal uplift factor to apply when a product/category falls inside an event window.
    /// Defaults to 1.0 (no effect) when no rule matches. Lookups are accent- and case-insensitive.
    /// </summary>
    /// <param name="categoryName">Product category canonical name (e.g. "Alimentaire", "Textile"). Null/empty → 1.0.</param>
    /// <param name="date">Date to evaluate.</param>
    double GetSeasonalFactor(string? categoryName, DateTime date);

    /// <summary>Return events strictly within the next <paramref name="daysAhead"/> days starting from today (Africa/Tunis).</summary>
    IReadOnlyList<TunisianEvent> GetUpcomingEvents(int daysAhead);
}
