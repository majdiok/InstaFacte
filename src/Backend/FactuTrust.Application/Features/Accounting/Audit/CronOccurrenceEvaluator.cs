using System.Globalization;

namespace FactuTrust.Application.Features.Accounting.Audit;

/// <summary>
/// Évalue « une occurrence cron a-t-elle été franchie depuis la dernière exécution ? » pour les
/// planifications de contrôle comptable.
///
/// <para><b>Pourquoi un évaluateur maison.</b> Hangfire 1.8 internalise Cronos : aucun analyseur
/// cron public n'est accessible depuis les paquets déjà référencés. Plutôt que d'ajouter une
/// dépendance pour quelques dizaines de lignes, on évalue ici le sous-ensemble standard à cinq
/// champs, qui couvre tout ce qu'une planification de contrôle peut exprimer.</para>
///
/// <para><b>Sémantique.</b> On ne cherche pas « la prochaine occurrence » mais « existe-t-il une
/// minute correspondant à l'expression dans l'intervalle ]dernière exécution, maintenant] ». Cette
/// formulation est robuste au fait que le déclencheur Hangfire est quotidien : une occurrence
/// tombée dans la nuit est bien détectée au passage suivant, sans être perdue ni rejouée.</para>
///
/// <para><b>Granularité réelle.</b> Le déclencheur étant quotidien, une expression sous-quotidienne
/// (« toutes les heures ») ne produit qu'une exécution par jour. C'est voulu : un contrôle
/// comptable complet n'a pas de sens à la fréquence horaire.</para>
///
/// <para>Champs pris en charge : minute, heure, jour du mois, mois, jour de la semaine.
/// Syntaxes : <c>*</c>, <c>5</c>, <c>1-5</c>, <c>*&#47;15</c>, <c>1-20&#47;5</c>, listes séparées
/// par des virgules, et les macros <c>@hourly</c>, <c>@daily</c>, <c>@midnight</c>, <c>@weekly</c>,
/// <c>@monthly</c>, <c>@yearly</c> / <c>@annually</c>. Dimanche vaut 0 <b>ou</b> 7.</para>
/// </summary>
public static class CronOccurrenceEvaluator
{
    /// <summary>
    /// Profondeur maximale de remontée, en jours. Au-delà, la planification est considérée due
    /// sans balayer l'intervalle : inutile de parcourir des mois de minutes pour conclure « oui ».
    /// </summary>
    private const int MaxLookbackDays = 40;

    /// <summary>
    /// Vrai si une occurrence de <paramref name="expression"/> tombe dans
    /// ]<paramref name="lastRunUtc"/>, <paramref name="nowUtc"/>].
    /// <para><paramref name="lastRunUtc"/> à null signifie « jamais exécutée » : due immédiatement.
    /// Une expression vide ou illisible renvoie <c>false</c> — une planification qu'on ne sait pas
    /// lire ne doit pas se déclencher au hasard.</para>
    /// </summary>
    public static bool HasOccurrenceBetween(string? expression, DateTime? lastRunUtc, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        if (!TryParse(expression, out var schedule))
            return false;

        if (lastRunUtc is not { } lastRun)
            return true;

        if (lastRun >= nowUtc)
            return false;

        var floor = nowUtc.AddDays(-MaxLookbackDays);
        if (lastRun < floor)
            return true;

        // Balayage minute par minute sur l'intervalle ouvert à gauche, fermé à droite.
        var cursor = TruncateToMinute(lastRun).AddMinutes(1);
        var end = TruncateToMinute(nowUtc);
        while (cursor <= end)
        {
            if (schedule.Matches(cursor))
                return true;
            cursor = cursor.AddMinutes(1);
        }

        return false;
    }

    /// <summary>Vrai si l'expression est exploitable. Sert à valider une saisie utilisateur.</summary>
    public static bool IsValid(string? expression) =>
        !string.IsNullOrWhiteSpace(expression) && TryParse(expression, out _);

    private static DateTime TruncateToMinute(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    private static bool TryParse(string expression, out CronSchedule schedule)
    {
        schedule = default!;

        var normalized = ExpandMacro(expression.Trim());
        var fields = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
            return false;

        if (!TryParseField(fields[0], 0, 59, out var minutes)) return false;
        if (!TryParseField(fields[1], 0, 23, out var hours)) return false;
        if (!TryParseField(fields[2], 1, 31, out var daysOfMonth)) return false;
        if (!TryParseField(fields[3], 1, 12, out var months)) return false;
        if (!TryParseField(fields[4], 0, 7, out var daysOfWeek)) return false;

        // 7 et 0 désignent tous deux dimanche : on normalise pour que la comparaison soit directe.
        if (daysOfWeek.Contains(7))
        {
            daysOfWeek.Remove(7);
            daysOfWeek.Add(0);
        }

        schedule = new CronSchedule(
            minutes,
            hours,
            daysOfMonth,
            months,
            daysOfWeek,
            dayOfMonthRestricted: fields[2] != "*",
            dayOfWeekRestricted: fields[4] != "*");
        return true;
    }

    private static string ExpandMacro(string expression) => expression.ToLowerInvariant() switch
    {
        "@hourly" => "0 * * * *",
        "@daily" or "@midnight" => "0 0 * * *",
        "@weekly" => "0 0 * * 0",
        "@monthly" => "0 0 1 * *",
        "@yearly" or "@annually" => "0 0 1 1 *",
        _ => expression
    };

    private static bool TryParseField(string field, int min, int max, out HashSet<int> values)
    {
        values = new HashSet<int>();

        foreach (var part in field.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var step = 1;
            var rangePart = part;

            var slash = part.IndexOf('/');
            if (slash >= 0)
            {
                rangePart = part[..slash];
                if (!int.TryParse(part[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out step)
                    || step <= 0)
                    return false;
            }

            int from, to;
            if (rangePart == "*")
            {
                from = min;
                to = max;
            }
            else
            {
                var dash = rangePart.IndexOf('-');
                if (dash >= 0)
                {
                    if (!int.TryParse(rangePart[..dash], NumberStyles.None, CultureInfo.InvariantCulture, out from)
                        || !int.TryParse(rangePart[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out to))
                        return false;
                }
                else
                {
                    if (!int.TryParse(rangePart, NumberStyles.None, CultureInfo.InvariantCulture, out from))
                        return false;
                    // « 5/15 » se lit « à partir de 5, tous les 15 » — comme dans la spécification cron.
                    to = slash >= 0 ? max : from;
                }
            }

            if (from < min || to > max || from > to)
                return false;

            for (var v = from; v <= to; v += step)
                values.Add(v);
        }

        return values.Count > 0;
    }

    private sealed class CronSchedule
    {
        private readonly HashSet<int> _minutes;
        private readonly HashSet<int> _hours;
        private readonly HashSet<int> _daysOfMonth;
        private readonly HashSet<int> _months;
        private readonly HashSet<int> _daysOfWeek;
        private readonly bool _dayOfMonthRestricted;
        private readonly bool _dayOfWeekRestricted;

        public CronSchedule(
            HashSet<int> minutes,
            HashSet<int> hours,
            HashSet<int> daysOfMonth,
            HashSet<int> months,
            HashSet<int> daysOfWeek,
            bool dayOfMonthRestricted,
            bool dayOfWeekRestricted)
        {
            _minutes = minutes;
            _hours = hours;
            _daysOfMonth = daysOfMonth;
            _months = months;
            _daysOfWeek = daysOfWeek;
            _dayOfMonthRestricted = dayOfMonthRestricted;
            _dayOfWeekRestricted = dayOfWeekRestricted;
        }

        public bool Matches(DateTime instant)
        {
            if (!_minutes.Contains(instant.Minute)) return false;
            if (!_hours.Contains(instant.Hour)) return false;
            if (!_months.Contains(instant.Month)) return false;

            var dayOfMonthOk = _daysOfMonth.Contains(instant.Day);
            var dayOfWeekOk = _daysOfWeek.Contains((int)instant.DayOfWeek);

            // Règle cron historique : quand les DEUX champs de jour sont restreints, ils sont en OU,
            // pas en ET — « 0 0 1 * 1 » vaut « le 1er du mois OU tous les lundis ».
            if (_dayOfMonthRestricted && _dayOfWeekRestricted)
                return dayOfMonthOk || dayOfWeekOk;

            return dayOfMonthOk && dayOfWeekOk;
        }
    }
}
