using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Jours fériés tunisiens par défaut (fixes + islamiques estimés) pour l'amorçage tenant.
/// Les dates islamiques sont indicatives (±1 jour selon observation lunaire).
/// </summary>
public static class PayrollPublicHolidaySeed
{
    public static IReadOnlyList<PayrollPublicHoliday> GetDefaultsForYear(int year)
    {
        var byDate = new Dictionary<DateTime, PayrollPublicHoliday>();

        foreach (var holiday in GetFixedHolidays(year).Concat(GetIslamicHolidays(year)))
        {
            var date = holiday.Date.Date;
            if (byDate.TryGetValue(date, out var existing))
                byDate[date] = MergeHolidays(existing, holiday);
            else
                byDate[date] = holiday;
        }

        return byDate.Values.OrderBy(h => h.Date).ToList();
    }

    public static IReadOnlyList<PayrollPublicHoliday> GetDefaultsForYears(IEnumerable<int> years) =>
        years.SelectMany(GetDefaultsForYear).ToList();

    public static IReadOnlyList<int> SupportedSeedYears { get; } = [2024, 2025, 2026, 2027, 2028];

    private static IEnumerable<PayrollPublicHoliday> GetFixedHolidays(int year)
    {
        foreach (var (month, day, label) in FixedHolidays)
        {
            var result = PayrollPublicHoliday.Create(
                year,
                new DateTime(year, month, day),
                label,
                PublicHolidayKind.Fixed,
                isPaid: true,
                isEstimated: false,
                decreeReference: "Calendrier des fêtes légales — République tunisienne");
            if (result.IsSuccess)
                yield return result.Value;
        }
    }

    private static PayrollPublicHoliday MergeHolidays(PayrollPublicHoliday existing, PayrollPublicHoliday incoming)
    {
        var mergedLabel = $"{existing.Label} / {incoming.Label}";
        var mergedKind = existing.Kind == PublicHolidayKind.Fixed || incoming.Kind == PublicHolidayKind.Fixed
            ? PublicHolidayKind.Fixed
            : PublicHolidayKind.Islamic;
        var mergedRefs = new[] { existing.DecreeReference, incoming.DecreeReference }
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct()
            .ToList();
        var mergedRef = mergedRefs.Count > 0 ? string.Join(" ; ", mergedRefs) : null;

        var result = PayrollPublicHoliday.Create(
            existing.Year,
            existing.Date,
            mergedLabel,
            mergedKind,
            isPaid: existing.IsPaid && incoming.IsPaid,
            isEstimated: existing.IsEstimated || incoming.IsEstimated,
            decreeReference: mergedRef);

        return result.IsSuccess ? result.Value : existing;
    }

    private static IEnumerable<PayrollPublicHoliday> GetIslamicHolidays(int year)
    {
        if (!IslamicHolidaysByYear.TryGetValue(year, out var entries))
            yield break;

        foreach (var entry in entries)
        {
            var result = PayrollPublicHoliday.Create(
                year,
                entry.Date,
                entry.Label,
                PublicHolidayKind.Islamic,
                isPaid: true,
                isEstimated: true,
                decreeReference: "Date estimée — à confirmer par décret (observation lunaire)");
            if (result.IsSuccess)
                yield return result.Value;
        }
    }

    private static readonly (int Month, int Day, string Label)[] FixedHolidays =
    [
        (1, 1, "Nouvel An"),
        (1, 14, "Fête de la Révolution et de la Jeunesse"),
        (3, 20, "Fête de l'Indépendance"),
        (4, 9, "Journée des Martyrs"),
        (5, 1, "Fête du Travail"),
        (7, 25, "Fête de la République"),
        (8, 13, "Fête de la Femme"),
        (10, 15, "Fête de l'Évacuation")
    ];

    private static readonly Dictionary<int, (DateTime Date, string Label)[]> IslamicHolidaysByYear =
        new()
        {
            [2024] =
            [
                (new DateTime(2024, 4, 10), "Aïd El-Fitr (1er jour)"),
                (new DateTime(2024, 4, 11), "Aïd El-Fitr (2e jour)"),
                (new DateTime(2024, 6, 16), "Aïd El-Adha (1er jour)"),
                (new DateTime(2024, 6, 17), "Aïd El-Adha (2e jour)"),
                (new DateTime(2024, 7, 7), "Ras El Am El Hijri"),
                (new DateTime(2024, 9, 15), "Mouled (Naissance du Prophète)")
            ],
            [2025] =
            [
                (new DateTime(2025, 3, 31), "Aïd El-Fitr (1er jour)"),
                (new DateTime(2025, 4, 1), "Aïd El-Fitr (2e jour)"),
                (new DateTime(2025, 6, 6), "Aïd El-Adha (1er jour)"),
                (new DateTime(2025, 6, 7), "Aïd El-Adha (2e jour)"),
                (new DateTime(2025, 6, 26), "Ras El Am El Hijri"),
                (new DateTime(2025, 9, 4), "Mouled (Naissance du Prophète)")
            ],
            [2026] =
            [
                (new DateTime(2026, 3, 20), "Aïd El-Fitr (1er jour)"),
                (new DateTime(2026, 3, 21), "Aïd El-Fitr (2e jour)"),
                (new DateTime(2026, 5, 27), "Aïd El-Adha (1er jour)"),
                (new DateTime(2026, 5, 28), "Aïd El-Adha (2e jour)"),
                (new DateTime(2026, 6, 16), "Ras El Am El Hijri"),
                (new DateTime(2026, 8, 25), "Mouled (Naissance du Prophète)")
            ],
            [2027] =
            [
                (new DateTime(2027, 3, 9), "Aïd El-Fitr (1er jour)"),
                (new DateTime(2027, 3, 10), "Aïd El-Fitr (2e jour)"),
                (new DateTime(2027, 5, 16), "Aïd El-Adha (1er jour)"),
                (new DateTime(2027, 5, 17), "Aïd El-Adha (2e jour)"),
                (new DateTime(2027, 6, 5), "Ras El Am El Hijri"),
                (new DateTime(2027, 8, 14), "Mouled (Naissance du Prophète)")
            ],
            [2028] =
            [
                (new DateTime(2028, 2, 26), "Aïd El-Fitr (1er jour)"),
                (new DateTime(2028, 2, 27), "Aïd El-Fitr (2e jour)"),
                (new DateTime(2028, 5, 5), "Aïd El-Adha (1er jour)"),
                (new DateTime(2028, 5, 6), "Aïd El-Adha (2e jour)"),
                (new DateTime(2028, 5, 25), "Ras El Am El Hijri"),
                (new DateTime(2028, 8, 3), "Mouled (Naissance du Prophète)")
            ]
        };
}
