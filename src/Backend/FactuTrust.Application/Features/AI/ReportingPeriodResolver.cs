namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Resolves reporting date ranges from presets using a single clock and Tunisia (Africa/Tunis) calendar dates.
/// </summary>
public static class ReportingPeriodResolver
{
    public const string PresetLastCompletedQuarter = "last_completed_quarter";
    public const string PresetCurrentQuarter = "current_quarter";
    public const string PresetLast30Days = "last_30_days";
    public const string PresetYearToDate = "year_to_date";
    /// <summary>Fenêtre pluriannuelle : indispensable à un état regroupé PAR ANNÉE, qu'« année en
    /// cours » réduirait à une seule ligne.</summary>
    public const string PresetLastFiveYears = "last_5_years";
    public const string PresetCurrentMonth = "current_month";
    public const string PresetLastMonth = "last_month";
    public const string PresetLast7Days = "last_7_days";
    public const string PresetToday = "today";
    public const string PresetYesterday = "yesterday";

    public static readonly IReadOnlyList<string> ValidPresets = new[]
    {
        PresetLastCompletedQuarter,
        PresetCurrentQuarter,
        PresetLast30Days,
        PresetYearToDate,
        PresetLastFiveYears,
        PresetCurrentMonth,
        PresetLastMonth,
        PresetLast7Days,
        PresetToday,
        PresetYesterday
    };

    /// <summary>
    /// Returns Tunisia local date for the given instant (UTC-based clock).
    /// </summary>
    public static DateOnly GetTodayInTunisia(TimeProvider timeProvider)
    {
        var tz = GetTunisiaTimeZone();
        var utcNow = timeProvider.GetUtcNow();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, tz);
        return DateOnly.FromDateTime(local);
    }

    public static ReportingPeriodResolution Resolve(string preset, TimeProvider timeProvider)
    {
        var p = (preset ?? string.Empty).Trim().ToLowerInvariant();
        var today = GetTodayInTunisia(timeProvider);

        return p switch
        {
            PresetLastCompletedQuarter => ResolveLastCompletedQuarter(today),
            PresetCurrentQuarter => ResolveCurrentQuarter(today),
            PresetLast30Days => ResolveLast30Days(today),
            PresetYearToDate => ResolveYearToDate(today),
            PresetLastFiveYears => ResolveLastFiveYears(today),
            PresetCurrentMonth => ResolveCurrentMonth(today),
            PresetLastMonth => ResolveLastMonth(today),
            PresetLast7Days => ResolveLast7Days(today),
            PresetToday => ResolveToday(today),
            PresetYesterday => ResolveYesterday(today),
            _ => throw new ArgumentException($"Preset inconnu : {preset}. Valeurs : {string.Join(", ", ValidPresets)}.", nameof(preset))
        };
    }

    public static (DateOnly From, DateOnly To) GetLastCompletedQuarterRange(DateOnly today)
    {
        var currentQuarter = QuarterOfYear(today);
        var firstOfCurrentQuarter = FirstDayOfQuarter(today.Year, currentQuarter);
        var lastDayOfPreviousQuarter = firstOfCurrentQuarter.AddDays(-1);
        var previousQuarter = QuarterOfYear(lastDayOfPreviousQuarter);
        var firstOfPreviousQuarter = FirstDayOfQuarter(lastDayOfPreviousQuarter.Year, previousQuarter);
        return (firstOfPreviousQuarter, lastDayOfPreviousQuarter);
    }

    private static ReportingPeriodResolution ResolveLastCompletedQuarter(DateOnly today)
    {
        var (from, to) = GetLastCompletedQuarterRange(today);
        return new ReportingPeriodResolution(
            from,
            to,
            $"Dernier trimestre civil complété ({FormatQuarterLabel(from)} — {FormatQuarterLabel(to)})");
    }

    private static ReportingPeriodResolution ResolveCurrentQuarter(DateOnly today)
    {
        var q = QuarterOfYear(today);
        var from = FirstDayOfQuarter(today.Year, q);
        return new ReportingPeriodResolution(
            from,
            today,
            $"Trimestre civil en cours ({FormatQuarterLabel(from)} — {today:dd/MM/yyyy})");
    }

    private static ReportingPeriodResolution ResolveLast30Days(DateOnly today)
    {
        var from = today.AddDays(-29);
        return new ReportingPeriodResolution(from, today, "30 derniers jours (glissants)");
    }

    private static ReportingPeriodResolution ResolveYearToDate(DateOnly today)
    {
        var from = new DateOnly(today.Year, 1, 1);
        return new ReportingPeriodResolution(from, today, $"Depuis le 1er janvier {today.Year}");
    }

    /// <summary>Année en cours plus les quatre précédentes, en années civiles pleines.</summary>
    private static ReportingPeriodResolution ResolveLastFiveYears(DateOnly today)
    {
        var from = new DateOnly(today.Year - 4, 1, 1);
        return new ReportingPeriodResolution(from, today, $"5 dernières années ({from.Year} — {today.Year})");
    }

    private static ReportingPeriodResolution ResolveCurrentMonth(DateOnly today)
    {
        var from = new DateOnly(today.Year, today.Month, 1);
        return new ReportingPeriodResolution(from, today, $"Mois en cours ({from:dd/MM/yyyy} — {today:dd/MM/yyyy})");
    }

    private static ReportingPeriodResolution ResolveLastMonth(DateOnly today)
    {
        var firstOfCurrent = new DateOnly(today.Year, today.Month, 1);
        var lastOfPrevious = firstOfCurrent.AddDays(-1);
        var firstOfPrevious = new DateOnly(lastOfPrevious.Year, lastOfPrevious.Month, 1);
        return new ReportingPeriodResolution(firstOfPrevious, lastOfPrevious,
            $"Mois précédent ({firstOfPrevious:dd/MM/yyyy} — {lastOfPrevious:dd/MM/yyyy})");
    }

    private static ReportingPeriodResolution ResolveLast7Days(DateOnly today)
    {
        var from = today.AddDays(-6);
        return new ReportingPeriodResolution(from, today, "7 derniers jours (glissants)");
    }

    private static ReportingPeriodResolution ResolveToday(DateOnly today)
        => new ReportingPeriodResolution(today, today, $"Aujourd'hui ({today:dd/MM/yyyy})");

    private static ReportingPeriodResolution ResolveYesterday(DateOnly today)
    {
        var d = today.AddDays(-1);
        return new ReportingPeriodResolution(d, d, $"Hier ({d:dd/MM/yyyy})");
    }

    private static int QuarterOfYear(DateOnly d)
        => (d.Month - 1) / 3 + 1;

    private static DateOnly FirstDayOfQuarter(int year, int quarter)
    {
        var month = (quarter - 1) * 3 + 1;
        return new DateOnly(year, month, 1);
    }

    private static string FormatQuarterLabel(DateOnly d)
    {
        var q = QuarterOfYear(d);
        return q switch
        {
            1 => $"T1 {d.Year} (janv.–mars)",
            2 => $"T2 {d.Year} (avr.–juin)",
            3 => $"T3 {d.Year} (juil.–sept.)",
            _ => $"T4 {d.Year} (oct.–déc.)"
        };
    }

    private static TimeZoneInfo GetTunisiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Tunis");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time");
        }
    }
}

public sealed record ReportingPeriodResolution(
    DateOnly FromDate,
    DateOnly ToDate,
    string Label);
