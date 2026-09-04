using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Application.Features.Studio.Ai;

namespace FactuTrust.Application.Features.AI;

public static class AssistantVisibleContentFormatter
{
    private static readonly Regex JsonFenceRegex = new("```json[\\s\\S]*?```", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FtMetaFenceRegex = new("```ft-meta[\\s\\S]*?```", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GenericCodeFenceRegex = new("```[\\s\\S]*?```", RegexOptions.Compiled);
    private static readonly Regex UnclosedFenceRegex = new(
        "```(?:json|ft-meta|dashboard)?[\\s\\S]*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Plafond de lignes humanisées dans le repli (parité avec HUMANIZE_MAX_ROWS du frontend).</summary>
    private const int MaxHumanizedRows = 50;

    /// <summary>
    /// Détecte les identifiants d'outils internes (snake_case : get_*, resolve_*, forecast_*, …) dans le texte
    /// visible. Construit une fois depuis le registre. Ces identifiants n'apparaissent jamais en prose française
    /// normale → aucun faux positif ; sert de garde-fou contre les fuites de noms d'outils.
    /// </summary>
    private static readonly Regex InternalToolNameRegex = BuildInternalToolNameRegex();

    // NB : construit via la MÉTHODE (pas le champ) partout où c'est référencé par un initialiseur
    // statique — l'ordre textuel des initialiseurs C# rendrait sinon le champ encore null.
    private static readonly string InternalToolNamePattern = BuildInternalToolNamePattern();

    private static string BuildInternalToolNamePattern()
    {
        var names = AiToolRegistry.All
            .Select(t => t.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n.Contains('_'))
            .Distinct()
            .OrderByDescending(n => n.Length)
            .Select(Regex.Escape);
        return @"\b(" + string.Join("|", names) + @")\b";
    }

    private static Regex BuildInternalToolNameRegex() =>
        new(BuildInternalToolNamePattern(), RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>True si le texte visible contient un nom d'outil interne (fuite à neutraliser).</summary>
    public static bool ContainsInternalToolName(string? content) =>
        !string.IsNullOrEmpty(content) && InternalToolNameRegex.IsMatch(content);

    private const string UnknownInternalTokenPattern =
        @"\b(?:get|forecast|analyze|simulate|generate|create|record|propose|resolve|compliance|studio)_[a-z0-9]+(?:_[a-z0-9]+)*\b";

    /// <summary>
    /// Tokens snake_case à préfixe interne NON présents dans le registre (variantes hallucinées par le
    /// modèle, ex. un outil renommé). Préfixes volontairement restreints pour ne jamais toucher un
    /// identifiant métier légitime (code produit, etc.) dans la prose.
    /// </summary>
    private static readonly Regex UnknownInternalTokenRegex = new(
        UnknownInternalTokenPattern,
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Variantes entourées de backticks (`nom`) : backticks absorbés par la substitution.</summary>
    private static readonly Regex BacktickedToolNameRegex = new(
        "`\\s*" + InternalToolNamePattern + "\\s*`",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BacktickedUnknownTokenRegex = new(
        "`\\s*" + UnknownInternalTokenPattern + "\\s*`",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// SUBSTITUE les identifiants d'outils internes par leur libellé métier français au lieu de jeter la
    /// réponse entière : les fuites du petit modèle (« utilisez forecast_product_demand ») deviennent de la
    /// prose lisible sans perdre le contenu utile. Backticks adjacents absorbés (`nom` → libellé).
    /// Les fences ```json sont préservées telles quelles (payloads techniques légitimes, ex. dashboard).
    /// </summary>
    public static string SanitizeInternalToolNames(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;
        if (!InternalToolNameRegex.IsMatch(content) && !UnknownInternalTokenRegex.IsMatch(content))
            return content;

        // Ne substituer QUE hors fences ```...``` : le JSON de dashboard/outils doit rester intact.
        return ReplaceOutsideCodeFences(content, segment =>
        {
            var replaced = BacktickedToolNameRegex.Replace(
                segment,
                m => AiToolFrenchLabels.Describe(m.Groups[1].Value.ToLowerInvariant()));
            replaced = BacktickedUnknownTokenRegex.Replace(
                replaced,
                m => AiToolFrenchLabels.GenericLabel);
            replaced = InternalToolNameRegex.Replace(
                replaced,
                m => AiToolFrenchLabels.Describe(m.Value.ToLowerInvariant()));
            return UnknownInternalTokenRegex.Replace(replaced, _ => AiToolFrenchLabels.GenericLabel);
        });
    }

    private static string ReplaceOutsideCodeFences(string content, Func<string, string> transform)
    {
        var sb = new System.Text.StringBuilder(content.Length);
        var index = 0;
        foreach (Match fence in GenericCodeFenceRegex.Matches(content))
        {
            if (fence.Index > index)
                sb.Append(transform(content[index..fence.Index]));
            sb.Append(fence.Value);
            index = fence.Index + fence.Length;
        }
        if (index < content.Length)
            sb.Append(transform(content[index..]));
        return sb.ToString();
    }

    /// <summary>
    /// Assainissement de prose visible : substitution des noms d'outils puis retrait des écritures CJK
    /// (hors fences). Idempotent. Utilisé pour le streaming live, le corps final et les canaux.
    /// </summary>
    public static string SanitizeVisibleProse(string? content) =>
        StripDisallowedScripts(SanitizeInternalToolNames(content));

    /// <summary>
    /// True si le texte contient au moins une rune CJK (idéogrammes, kana, hangul, ponctuation CJK),
    /// y compris les extensions hors BMP. L'arabe et le latin ne matchent pas.
    /// </summary>
    public static bool ContainsCjkScript(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return false;
        foreach (var rune in content.EnumerateRunes())
        {
            if (IsCjkRune(rune))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Retire les écritures CJK de la prose visible (hors fences <c>```</c>) pour bloquer la dérive
    /// linguistique des petits Qwen. Latin, chiffres, ponctuation occidentale et arabe sont conservés.
    /// Les lignes devenues vides / ponctuation orpheline après retrait sont supprimées ; les sauts de
    /// paragraphe français sont préservés. Idempotent ; no-op si aucun CJK.
    /// </summary>
    public static string StripDisallowedScripts(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;
        if (!ContainsCjkScript(content))
            return content;

        return ReplaceOutsideCodeFences(content, StripCjkFromProseSegment);
    }

    private static string StripCjkFromProseSegment(string segment)
    {
        var lines = segment.Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            if (!ContainsCjkScript(line))
            {
                kept.Add(line);
                continue;
            }

            var stripped = CollapseHorizontalWhitespace(StripCjkRunes(line));
            if (IsOrphanPunctuationLine(stripped))
                continue;
            kept.Add(stripped);
        }

        return MultiBlankLineRegex.Replace(string.Join("\n", kept), "\n\n");
    }

    private static string StripCjkRunes(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (!IsCjkRune(rune))
                sb.Append(rune);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Idéogrammes unifiés + extensions, kana, hangul, ponctuation CJK (U+3000–U+303F),
    /// et plan complémentaire (Ext. B–F). Volontairement hors arabe / cyrillique / thaï.
    /// </summary>
    private static bool IsCjkRune(Rune rune)
    {
        var value = rune.Value;
        return value is (>= 0x3400 and <= 0x4DBF)
            or (>= 0x4E00 and <= 0x9FFF)
            or (>= 0xF900 and <= 0xFAFF)
            or (>= 0x3040 and <= 0x309F)
            or (>= 0x30A0 and <= 0x30FF)
            or (>= 0xAC00 and <= 0xD7AF)
            or (>= 0x3000 and <= 0x303F)
            or (>= 0x20000 and <= 0x2FA1F);
    }

    private static string CollapseHorizontalWhitespace(string line) =>
        HorizontalWhitespaceRunRegex.Replace(line, " ");

    private static bool IsOrphanPunctuationLine(string line)
    {
        foreach (var rune in line.EnumerateRunes())
        {
            if (Rune.IsLetter(rune) || Rune.IsDigit(rune))
                return false;
        }
        return true;
    }

    /// <summary>Toute mention du « JSON » (le spec interne n'a jamais à apparaître côté Studio).</summary>
    private static readonly Regex JsonWordRegex = new(@"\bjson\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MultiBlankLineRegex = new(@"(?:[ \t]*\n){3,}", RegexOptions.Compiled);
    private static readonly Regex HorizontalWhitespaceRunRegex = new(@"[ \t]{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Redaction CIBLÉE pour l'assistant Studio (StudioBuilder) : retire les fences techniques puis, ligne
    /// par ligne, toute ligne nommant un outil interne (<c>studio_generate_*</c>, etc.) ou mentionnant le
    /// « JSON » — tout en CONSERVANT la prose utile. Le petit modèle 3B fuit ces détails malgré le prompt.
    /// Si tout le contenu était interne, renvoie un court accusé générique (pas de bulle vide).
    /// </summary>
    public static string RedactStudioInternalLeaks(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content ?? string.Empty;

        var text = StripTechnicalFences(content);
        text = UnclosedFenceRegex.Replace(text, string.Empty);
        text = StudioTextToolCallRecovery.StripBareStudioJson(text);
        var kept = text
            .Split('\n')
            .Where(line => !InternalToolNameRegex.IsMatch(line) && !JsonWordRegex.IsMatch(line));

        var result = MultiBlankLineRegex.Replace(string.Join("\n", kept), "\n\n").Trim();
        return string.IsNullOrWhiteSpace(result) ? StudioRedactionFallback : result;
    }

    /// <summary>
    /// Texte de repli quand la redaction a tout retiré (réponse entièrement « interne »).
    ///
    /// Sa LONGUEUR est contractuelle : il doit dépasser
    /// <c>OllamaSettings.MinAssistantTextCharsForCompleteResponse</c>, sinon
    /// <see cref="HasMeaningfulAssistantText"/> le rejette et la réponse tombe dans le filet
    /// anti-silence — dont le libellé parle de création de système, à contretemps d'une demande
    /// d'état. Le repli précédent faisait 71 caractères pour un seuil de 80 : le piège se refermait
    /// mécaniquement. Neutre par ailleurs : il ne présume ni d'une table ni d'un état.
    /// Couvert par <c>AssistantVisibleContentFormatterTests</c>.
    /// </summary>
    public const string StudioRedactionFallback =
        "Je peux créer des tables et produire des états sur vos données. Dites-moi ce que vous "
        + "voulez obtenir : pour un état, la période et ce qu'il faut mesurer ; pour une table, ses champs.";

    public static int MeasureVisibleProseLength(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return 0;
        return StripTechnicalFences(content).Trim().Length;
    }

    public static bool HasMeaningfulAssistantText(string? content, int minChars) =>
        MeasureVisibleProseLength(content) >= Math.Clamp(minChars, 1, 4096);

    public static string EnhanceForDisplay(string? content, int minMeaningfulChars = 80)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content ?? string.Empty;

        // Toujours (indépendamment de la longueur de prose) : une fence {"sections":…} SANS title
        // n'est jamais un transport de dashboard légitime → convertie en puces lisibles au lieu
        // d'être affichée en JSON brut (« Indicateurs clés »).
        content = ReplaceSectionsOnlyFences(content);

        var visibleLen = MeasureVisibleProseLength(content);
        if (visibleLen >= minMeaningfulChars)
            return content;

        var humanized = TryHumanizeBusinessJsonFences(content);
        if (!string.IsNullOrWhiteSpace(humanized))
        {
            var prose = StripTechnicalFences(content).Trim();
            if (string.IsNullOrEmpty(prose))
                return humanized;
            if (visibleLen < minMeaningfulChars)
                return prose + "\n\n" + humanized;
        }

        return content;
    }

    private static string StripTechnicalFences(string content)
    {
        var stripped = JsonFenceRegex.Replace(content, string.Empty);
        stripped = FtMetaFenceRegex.Replace(stripped, string.Empty);
        stripped = GenericCodeFenceRegex.Replace(stripped, string.Empty);
        return stripped;
    }

    private static readonly Regex JsonFenceInnerRegex = new(
        "```json\\s*([\\s\\S]*?)\\s*```",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Remplace les fences <c>```json</c> de forme <c>{"sections":[…]}</c> SANS <c>title</c> par des puces
    /// lisibles (KPI : « - **Titre** : valeur unité »). Une fence AVEC title est le transport légitime du
    /// tableau de bord (<see cref="AssistantDashboardContent"/>) et reste strictement intacte.
    /// Parité frontend : <c>replaceSectionsOnlyFences</c> (assistant-message-display.ts).
    /// </summary>
    public static string ReplaceSectionsOnlyFences(string content) =>
        JsonFenceInnerRegex.Replace(content, m =>
        {
            var inner = m.Groups[1].Value.Trim();
            if (inner.Length == 0)
                return m.Value;
            try
            {
                using var doc = JsonDocument.Parse(inner);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return m.Value;

                // Écho des ARGUMENTS de generate_dashboard_config ({title, sections_json}) : jamais un
                // transport légitime, MÊME avec title — humanisé en puces (le vrai widget passe par
                // l'événement dashboard + l'appendice {title, sections}).
                if (root.TryGetProperty("sections_json", out var sectionsJson))
                {
                    if (sectionsJson.ValueKind == JsonValueKind.Array)
                        return HumanizeSectionEntries(sectionsJson) ?? m.Value;
                    if (sectionsJson.ValueKind == JsonValueKind.String)
                    {
                        try
                        {
                            using var innerDoc = JsonDocument.Parse(sectionsJson.GetString() ?? "");
                            if (innerDoc.RootElement.ValueKind == JsonValueKind.Array)
                                return HumanizeSectionEntries(innerDoc.RootElement) ?? m.Value;
                        }
                        catch (JsonException)
                        {
                            // sections_json illisible → fence conservée telle quelle
                        }
                    }
                    return m.Value;
                }

                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    return m.Value; // dashboard légitime (title+sections) — widget/appendice inchangés
                if (!root.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Array)
                    return m.Value;
                return HumanizeSectionEntries(sections) ?? m.Value;
            }
            catch (JsonException)
            {
                return m.Value;
            }
        });

    private static string? HumanizeSectionEntries(JsonElement sections)
    {
        var lines = new List<string>();
        foreach (var section in sections.EnumerateArray())
        {
            if (section.ValueKind != JsonValueKind.Object)
                continue;
            var title = FirstString(section, "title", "label", "name");
            if (string.IsNullOrWhiteSpace(title))
                continue;

            decimal? value = null;
            string? unit = null;
            if (section.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                value = FirstDecimal(data, "value", "amount", "total");
                unit = FirstString(data, "label", "unit", "currency");
            }

            lines.Add(value.HasValue
                ? $"- **{title}** : {FormatKpiValue(value.Value, unit)}"
                : $"- {title}");
        }
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private static string FormatKpiValue(decimal value, string? unit) =>
        string.Equals(unit, "TND", StringComparison.OrdinalIgnoreCase)
            ? FormatTnd(value)
            : FormatQuantity(value) + (string.IsNullOrWhiteSpace(unit) ? string.Empty : " " + unit);

    private static string? TryHumanizeBusinessJsonFences(string content)
    {
        var matchRegex = JsonFenceInnerRegex;
        var summaries = new List<string>();
        foreach (Match match in matchRegex.Matches(content))
        {
            var inner = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(inner))
                continue;
            var summary = HumanizeBusinessJson(inner);
            if (!string.IsNullOrWhiteSpace(summary))
                summaries.Add(summary);
        }
        return summaries.Count == 0 ? null : string.Join("\n\n", summaries);
    }

    private static string? HumanizeBusinessJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return HumanizeJsonArray(doc.RootElement);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                return HumanizeJsonObject(doc.RootElement);
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string HumanizeJsonArray(JsonElement arr)
    {
        if (arr.GetArrayLength() == 0)
            return "Aucune donnee pour cette periode.";

        var lines = new List<string>();
        foreach (var item in arr.EnumerateArray().Take(MaxHumanizedRows))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var name = FirstString(item, "productName", "clientName", "name", "label", "description", "groupKey");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var line = name;
            var qty = FirstDecimal(item, "quantity", "qty");
            if (qty.HasValue)
                line += $" ({FormatQuantity(qty.Value)})";
            var amount = FirstDecimal(item, "amount", "montant", "total", "ca", "revenue");
            if (amount.HasValue)
                line += " - " + FormatTnd(amount.Value);
            lines.Add("- " + line);
        }
        if (lines.Count == 0)
            return $"**{arr.GetArrayLength()}** ligne(s) de donnees.";
        var suffix = arr.GetArrayLength() > MaxHumanizedRows
            ? $"\n\n... et {arr.GetArrayLength() - MaxHumanizedRows} autre(s) ligne(s)."
            : string.Empty;
        return string.Join("\n", lines) + suffix;
    }

    /// <summary>
    /// Enveloppe d'un ÉTAT Studio : <c>{ title, sourceLabel, period, result: { columns, rows,
    /// totalRows, truncated } }</c>. Sans cette branche, un état parfaitement calculé — tableau déjà
    /// affiché à l'utilisateur — retombait sur « Je n'ai pas pu formuler une réponse complète à
    /// partir des données récupérées », parce que <c>rows</c> est imbriqué sous <c>result</c> et
    /// qu'aucune clé racine ne correspondait aux formes reconnues.
    ///
    /// Aucun chiffre n'est inventé : on ne fait qu'additionner des agrégats déjà produits par SQL,
    /// et uniquement lorsque le résultat est COMPLET — un total partiel présenté comme complet
    /// serait pire qu'une absence de total.
    /// </summary>
    private static string? HumanizeStudioReport(JsonElement obj, JsonElement result)
    {
        if (!result.TryGetProperty("columns", out var columns) || columns.ValueKind != JsonValueKind.Array)
            return null;

        var title = FirstString(obj, "title") ?? "État";
        var source = FirstString(obj, "sourceLabel", "source");
        var periodSuffix = TryFormatEnvelopePeriod(obj);
        var header = $"**{title}**" + (source is null ? string.Empty : $" — {source}");
        if (periodSuffix is not null)
            header += $", {periodSuffix}";

        var totalRows = (int)(FirstDecimal(result, "totalRows") ?? 0);
        var hasRows = result.TryGetProperty("rows", out var rows)
            && rows.ValueKind == JsonValueKind.Array
            && rows.GetArrayLength() > 0;

        if (totalRows == 0 || !hasRows)
            return header + " : aucune donnée sur la période retenue. "
                 + "Élargissez la période, ou choisissez une autre source.";

        var truncated = result.TryGetProperty("truncated", out var t) && t.ValueKind == JsonValueKind.True;
        var measures = columns.EnumerateArray()
            .Where(c => string.Equals(FirstString(c, "kind"), "measure", StringComparison.OrdinalIgnoreCase))
            .Select(c => (Key: FirstString(c, "key"), Label: FirstString(c, "label")))
            .Where(m => m.Key is not null)
            .ToList();
        var dimensions = columns.EnumerateArray()
            .Where(c => !string.Equals(FirstString(c, "kind"), "measure", StringComparison.OrdinalIgnoreCase))
            .Select(c => FirstString(c, "key"))
            .Where(k => k is not null)
            .ToList();

        var parts = new List<string> { $"{header} : **{totalRows}** ligne(s)." };

        // Totaux — seulement sur un résultat complet. Les mesures n'ont pas d'unité connue ici
        // (ReportColumn ne porte que clé/libellé/nature) : on formate le nombre sans y apposer
        // « TND », qui serait faux sur une quantité.
        if (!truncated)
        {
            foreach (var measure in measures)
            {
                decimal sum = 0;
                var seen = false;
                foreach (var row in rows.EnumerateArray())
                {
                    var value = FirstDecimal(row, measure.Key!);
                    if (value is null)
                        continue;
                    sum += value.Value;
                    seen = true;
                }
                if (seen)
                    parts.Add($"{measure.Label ?? measure.Key} : **{FormatQuantity(sum)}**.");
            }
        }
        else
        {
            parts.Add("Résultat tronqué : le total n'est pas calculé sur un extrait.");
        }

        // Ligne de tête : le résultat arrive déjà trié par l'état (mesure décroissante, ou
        // chronologie pour une série temporelle).
        var first = rows.EnumerateArray().First();
        var label = string.Join(" · ", dimensions
            .Select(k => FirstString(first, k!))
            .Where(v => !string.IsNullOrWhiteSpace(v)));
        var lead = measures.Count > 0 ? FirstDecimal(first, measures[0].Key!) : null;
        if (!string.IsNullOrWhiteSpace(label))
            parts.Add(lead is null
                ? $"Première ligne : {label}."
                : $"Première ligne : {label} ({FormatQuantity(lead.Value)}).");

        return string.Join(" ", parts);
    }

    private static string? HumanizeJsonObject(JsonElement obj)
    {
        // Un état Studio d'abord : sa forme est connue et ses chiffres sont exacts.
        if (obj.TryGetProperty("result", out var studioResult) && studioResult.ValueKind == JsonValueKind.Object)
        {
            var studio = HumanizeStudioReport(obj, studioResult);
            if (studio is not null)
                return studio;
        }

        // Enveloppe CA agrégé : { totalRevenue, rowCount, currency, groupBy, period, rows: [...] }.
        // On affiche le total DÉTERMINISTE de l'outil (jamais recalculé) puis la ventilation.
        if (obj.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
        {
            var total = FirstDecimal(obj, "totalRevenue", "total", "ca");
            if (total.HasValue)
            {
                var periodSuffix = TryFormatEnvelopePeriod(obj);
                if (rowsEl.GetArrayLength() == 0)
                    return periodSuffix is null
                        ? "Aucune vente sur la periode."
                        : $"Aucune vente sur la periode {periodSuffix}.";
                var rowCount = FirstDecimal(obj, "rowCount") ?? rowsEl.GetArrayLength();
                var header = periodSuffix is null
                    ? $"CA total : **{FormatTnd(total.Value)}** ({(int)rowCount} ligne(s))."
                    : $"CA total {periodSuffix} : **{FormatTnd(total.Value)}** ({(int)rowCount} ligne(s)).";
                return header + "\n" + HumanizeJsonArray(rowsEl);
            }
        }

        var parts = new List<string>();
        var amount = FirstDecimal(obj, "ca", "montant", "amount", "total", "totalAmount", "revenue");
        if (amount.HasValue)
            parts.Add($"Montant : **{FormatTnd(amount.Value)}**.");
        var period = FirstString(obj, "periode", "period", "periodLabel", "label");
        if (!string.IsNullOrWhiteSpace(period))
            parts.Add($"Periode : {period}.");
        var count = FirstDecimal(obj, "count", "nombre", "totalCount");
        if (count.HasValue && !amount.HasValue)
            parts.Add($"Nombre d'elements : {count.Value}.");
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    /// <summary>« du 03/06/2026 au 02/07/2026 » depuis le champ period {from,to} (yyyy-MM-dd), sinon null.</summary>
    private static string? TryFormatEnvelopePeriod(JsonElement obj)
    {
        if (!obj.TryGetProperty("period", out var period) || period.ValueKind != JsonValueKind.Object)
            return null;
        var from = FirstString(period, "from");
        var to = FirstString(period, "to");
        if (from is null || to is null)
            return null;
        return $"du {FormatIsoDateFr(from)} au {FormatIsoDateFr(to)}";
    }

    private static string FormatIsoDateFr(string isoDate) =>
        DateTime.TryParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : isoDate;

    private static string? FirstString(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s.Trim();
            }
        }
        return null;
    }

    private static decimal? FirstDecimal(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!obj.TryGetProperty(key, out var prop))
                continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d))
                return d;
            if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return null;
    }

    private static string FormatTnd(decimal value) =>
        value.ToString("N3", CultureInfo.GetCultureInfo("fr-FR")) + " TND";

    private static string FormatQuantity(decimal value) =>
        value % 1 == 0
            ? value.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))
            : value.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"));
}