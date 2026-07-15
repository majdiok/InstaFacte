using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces.Forecasting;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Forecasting.Calendar;

/// <summary>
/// Deterministic Tunisian commercial calendar.
/// Holidays:
///  • Civil (fixed) — 8 dates encoded in code.
///  • Religious lunar — 5 dates per year sourced from the embedded JSON table 2024–2030.
/// Commercial windows:
///  • Ramadan (30 days from RamadanStart),
///  • Aïd Al-Fitr (3 days starting at the Aïd date),
///  • Aïd Al-Adha (3 days starting at the Aïd date),
///  • Mouled / Ras El Am Hijri (1 day),
///  • Soldes Hiver (mi-janv → fin février),
///  • Soldes Été (mi-juillet → fin août),
///  • Rentrée Scolaire (15 août → 15 septembre),
///  • Black Friday (last Friday of November).
/// Climatic seasons: Été (juin–septembre), Hiver (décembre–février).
/// </summary>
public sealed class TunisianCalendarService : ITunisianCalendarService
{
    private static readonly TimeZoneInfo TunisTimeZone = ResolveTunisTimeZone();

    private readonly ILogger<TunisianCalendarService> _logger;
    private readonly ForecastingOptions _options;
    private readonly Dictionary<int, LunarYearTable> _lunarByYear;

    public TunisianCalendarService(
        IOptions<ForecastingOptions> options,
        ILogger<TunisianCalendarService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lunarByYear = LoadLunarTable(_logger);
    }

    public IReadOnlyList<TunisianEvent> GetEvents(DateTime from, DateTime to)
    {
        if (to < from) (from, to) = (to, from);

        var events = new List<TunisianEvent>();
        for (int year = from.Year; year <= to.Year; year++)
        {
            events.AddRange(BuildEventsForYear(year));
        }

        return events
            .Where(e => e.EndDate >= from.Date && e.StartDate <= to.Date)
            .OrderBy(e => e.StartDate)
            .ToList();
    }

    public bool IsHoliday(DateTime date)
    {
        var d = date.Date;
        return BuildEventsForYear(d.Year)
            .Any(e => e.IsHoliday && d >= e.StartDate && d <= e.EndDate);
    }

    public double GetSeasonalFactor(string? categoryName, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return 1.0;
        var canonical = NormaliseCategory(categoryName);
        if (!_options.SeasonalFactors.TryGetValue(canonical, out var perEvent) || perEvent.Count == 0)
            return 1.0;

        // Climatic season is also evaluated (Été/Hiver), so an event match wins over the broader season.
        var d = date.Date;
        var activeEvents = BuildEventsForYear(d.Year)
            .Where(e => d >= e.StartDate && d <= e.EndDate)
            .ToList();

        // Pick the strongest applicable factor (max).
        double best = 1.0;
        foreach (var ev in activeEvents)
        {
            if (perEvent.TryGetValue(ev.Code, out var factor) && factor > best)
                best = factor;
        }
        return best;
    }

    public IReadOnlyList<TunisianEvent> GetUpcomingEvents(int daysAhead)
    {
        if (daysAhead < 0) throw new ArgumentException("daysAhead must be ≥ 0", nameof(daysAhead));
        var todayTunis = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TunisTimeZone).Date;
        return GetEvents(todayTunis, todayTunis.AddDays(daysAhead));
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Event composition
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerable<TunisianEvent> BuildEventsForYear(int year)
    {
        // 1) Civil fixed holidays
        yield return Civil(year, 1, 1, TunisianEventCode.NouvelAn, "Nouvel An");
        yield return Civil(year, 1, 14, TunisianEventCode.FeteRevolution, "Fête de la Révolution et de la Jeunesse");
        yield return Civil(year, 3, 20, TunisianEventCode.FeteIndependance, "Fête de l'Indépendance");
        yield return Civil(year, 4, 9, TunisianEventCode.FeteMartyrs, "Journée des Martyrs");
        yield return Civil(year, 5, 1, TunisianEventCode.FeteTravail, "Fête du Travail");
        yield return Civil(year, 7, 25, TunisianEventCode.FeteRepublique, "Fête de la République");
        yield return Civil(year, 8, 13, TunisianEventCode.FeteFemme, "Fête de la Femme");
        yield return Civil(year, 10, 15, TunisianEventCode.FeteEvacuation, "Fête de l'Évacuation");

        // 2) Lunar holidays (from JSON table)
        if (_lunarByYear.TryGetValue(year, out var lunar))
        {
            // Ramadan = 30-day commercial window starting at RamadanStart (NOT itself a holiday)
            yield return new TunisianEvent
            {
                Code = TunisianEventCode.Ramadan,
                DisplayName = "Mois de Ramadan",
                StartDate = lunar.RamadanStart,
                EndDate = lunar.RamadanStart.AddDays(29),
                IsHoliday = false,
                IsCommercialWindow = true,
                Category = "ReligieuseLunaire"
            };

            yield return new TunisianEvent
            {
                Code = TunisianEventCode.AidAlFitr,
                DisplayName = "Aïd Al-Fitr",
                StartDate = lunar.AidAlFitr,
                EndDate = lunar.AidAlFitr.AddDays(2), // 3 jours fériés
                IsHoliday = true,
                IsCommercialWindow = true,
                Category = "ReligieuseLunaire"
            };

            yield return new TunisianEvent
            {
                Code = TunisianEventCode.AidAlAdha,
                DisplayName = "Aïd Al-Adha (Aïd El-Kébir)",
                StartDate = lunar.AidAlAdha,
                EndDate = lunar.AidAlAdha.AddDays(2),
                IsHoliday = true,
                IsCommercialWindow = true,
                Category = "ReligieuseLunaire"
            };

            yield return new TunisianEvent
            {
                Code = TunisianEventCode.RasElAmHijri,
                DisplayName = "Ras El Am Hijri (Nouvel An Hégirien)",
                StartDate = lunar.RasElAmHijri,
                EndDate = lunar.RasElAmHijri,
                IsHoliday = true,
                IsCommercialWindow = false,
                Category = "ReligieuseLunaire"
            };

            yield return new TunisianEvent
            {
                Code = TunisianEventCode.Mouled,
                DisplayName = "Mouled (Naissance du Prophète)",
                StartDate = lunar.Mouled,
                EndDate = lunar.Mouled,
                IsHoliday = true,
                IsCommercialWindow = true,
                Category = "ReligieuseLunaire"
            };
        }
        else
        {
            _logger.LogWarning(
                "No Tunisian lunar holiday data for year {Year}. Update Resources/tunisian-lunar-holidays.json. " +
                "Lunar-driven seasonal factors will fall back to 1.0 for this year.",
                year);
        }

        // 3) Commercial windows
        yield return Window(new DateTime(year, 1, 15), new DateTime(year, 2, 28),
            TunisianEventCode.SoldesHiver, "Soldes d'Hiver", "SoldesOfficielles");
        yield return Window(new DateTime(year, 7, 15), new DateTime(year, 8, 31),
            TunisianEventCode.SoldesEte, "Soldes d'Été", "SoldesOfficielles");
        yield return Window(new DateTime(year, 8, 15), new DateTime(year, 9, 15),
            TunisianEventCode.RentreeScolaire, "Rentrée Scolaire", "PeriodeCommerciale");

        // Black Friday — last Friday of November.
        var lastFriday = LastFridayOf(year, 11);
        yield return new TunisianEvent
        {
            Code = TunisianEventCode.BlackFriday,
            DisplayName = "Black Friday",
            StartDate = lastFriday,
            EndDate = lastFriday,
            IsHoliday = false,
            IsCommercialWindow = true,
            Category = "PeriodeCommerciale"
        };

        // 4) Climatic seasons
        yield return Window(new DateTime(year, 6, 1), new DateTime(year, 9, 30),
            TunisianEventCode.Ete, "Saison Été", "SaisonClimatique");
        // "Hiver" spans across Dec → Feb of next year — we model it inside one calendar year (Dec) only;
        // the same-year Jan/Feb part is captured as the next year's first 2 months.
        yield return Window(new DateTime(year, 1, 1), new DateTime(year, 2, 28),
            TunisianEventCode.Hiver, "Saison Hiver (déc → fév)", "SaisonClimatique");
        yield return Window(new DateTime(year, 12, 1), new DateTime(year, 12, 31),
            TunisianEventCode.Hiver + "_End", "Saison Hiver (déc → fév)", "SaisonClimatique");
    }

    private static TunisianEvent Civil(int year, int month, int day, string code, string displayName) => new()
    {
        Code = code,
        DisplayName = displayName,
        StartDate = new DateTime(year, month, day),
        EndDate = new DateTime(year, month, day),
        IsHoliday = true,
        IsCommercialWindow = false,
        Category = "FerieCivil"
    };

    private static TunisianEvent Window(DateTime start, DateTime end, string code, string displayName, string category) => new()
    {
        Code = code,
        DisplayName = displayName,
        StartDate = start,
        EndDate = end,
        IsHoliday = false,
        IsCommercialWindow = true,
        Category = category
    };

    private static DateTime LastFridayOf(int year, int month)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        var d = new DateTime(year, month, lastDay);
        while (d.DayOfWeek != DayOfWeek.Friday) d = d.AddDays(-1);
        return d;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Lunar table loading
    // ─────────────────────────────────────────────────────────────────────

    private sealed record LunarYearTable(int Year, DateTime RamadanStart, DateTime AidAlFitr, DateTime AidAlAdha, DateTime RasElAmHijri, DateTime Mouled);

    private static Dictionary<int, LunarYearTable> LoadLunarTable(ILogger logger)
    {
        var assembly = typeof(TunisianCalendarService).Assembly;
        // Search for the file name fragment so resource path is robust to namespace variations.
        var resName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("tunisian-lunar-holidays.json", StringComparison.OrdinalIgnoreCase));

        if (resName is null)
        {
            // Fallback: read from disk relative to the assembly directory.
            var asmDir = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
            var diskPath = Path.Combine(asmDir, "Services", "Forecasting", "Calendar", "Resources", "tunisian-lunar-holidays.json");
            if (File.Exists(diskPath))
                return ParseLunarJson(File.ReadAllText(diskPath));
            logger.LogError(
                "Tunisian lunar holiday JSON not found as embedded resource and not on disk at {Path}. " +
                "Lunar events will be empty until Resources/tunisian-lunar-holidays.json is published.",
                diskPath);
            return new Dictionary<int, LunarYearTable>();
        }

        using var stream = assembly.GetManifestResourceStream(resName)
            ?? throw new InvalidOperationException($"Embedded resource {resName} cannot be opened.");
        using var reader = new StreamReader(stream);
        return ParseLunarJson(reader.ReadToEnd());
    }

    private static Dictionary<int, LunarYearTable> ParseLunarJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<int, LunarYearTable>();
        if (!doc.RootElement.TryGetProperty("years", out var years) || years.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var y in years.EnumerateArray())
        {
            var year = y.GetProperty("year").GetInt32();
            var entry = new LunarYearTable(
                year,
                ParseDate(y, "ramadanStart"),
                ParseDate(y, "aidAlFitr"),
                ParseDate(y, "aidAlAdha"),
                ParseDate(y, "rasElAmHijri"),
                ParseDate(y, "mouled"));
            result[year] = entry;
        }
        return result;
    }

    private static DateTime ParseDate(JsonElement parent, string property)
    {
        var raw = parent.GetProperty(property).GetString();
        return DateTime.ParseExact(raw!, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static string NormaliseCategory(string name)
    {
        // Strip diacritics to make "Pâtisserie" match "Patisserie" in settings keys.
        var formD = name.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static TimeZoneInfo ResolveTunisTimeZone()
    {
        // Linux / .NET 8 = "Africa/Tunis"; Windows = "W. Central Africa Standard Time" or "Tunis Standard Time".
        foreach (var id in new[] { "Africa/Tunis", "Tunis Standard Time", "W. Central Africa Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Fallback: UTC+1 fixed.
        return TimeZoneInfo.CreateCustomTimeZone("Africa/Tunis-Fallback", TimeSpan.FromHours(1), "Africa/Tunis", "Africa/Tunis");
    }
}
