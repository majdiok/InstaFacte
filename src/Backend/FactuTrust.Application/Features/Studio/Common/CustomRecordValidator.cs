using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Validates an incoming custom record payload against its entity's active field definitions,
/// and produces the canonical JSON to persist. Unknown keys are rejected; values are coerced to
/// their declared type and checked against validation rules. This is the dynamic counterpart to
/// the static FluentValidation pipeline (which can only validate the command shape).
/// </summary>
public static class CustomRecordValidator
{
    public static Result<string> ValidateAndCanonicalize(
        IReadOnlyList<CustomFieldDefinition> fields,
        IReadOnlyDictionary<string, JsonNode?> data)
    {
        var activeKeys = fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

        // Reject unknown keys (anti arbitrary-JSON / reserved-name abuse).
        foreach (var key in data.Keys)
        {
            if (!activeKeys.Contains(key))
                return Result.Failure<string>(Error.Validation(key, $"Champ inconnu : « {key} »."));
        }

        var canonical = new JsonObject();

        foreach (var field in fields.OrderBy(f => f.SortOrder))
        {
            // Computed fields (AutoNumber today; formulas later) are derived server-side, never taken
            // from user input. The compute-on-write step injects them after this validation runs.
            if (IsComputed(field.FieldType))
                continue;

            data.TryGetValue(field.Key, out var raw);
            var isMissing = raw is null;

            if (isMissing)
            {
                if (field.IsRequired)
                    return Result.Failure<string>(Error.Validation(field.Key, $"« {field.Label} » est obligatoire."));
                continue;
            }

            var coerced = CoerceAndValidate(field, raw!, out var error);
            if (error is not null)
                return Result.Failure<string>(error);

            // null coerced for an optional empty value → skip persisting it.
            if (coerced is null)
            {
                if (field.IsRequired)
                    return Result.Failure<string>(Error.Validation(field.Key, $"« {field.Label} » est obligatoire."));
                continue;
            }

            canonical[field.Key] = coerced;
        }

        return Result.Success(canonical.ToJsonString());
    }

    private static JsonNode? CoerceAndValidate(CustomFieldDefinition field, JsonNode raw, out Error? error)
    {
        error = null;
        var rules = StudioFieldJson.ParseRules(field.ValidationRulesJson);

        switch (field.FieldType)
        {
            case CustomFieldType.Text:
            case CustomFieldType.MultilineText:
            {
                var s = AsString(raw);
                if (string.IsNullOrEmpty(s)) return null;
                if (rules?.MinLength is int min && s.Length < min)
                    return Fail(field, $"« {field.Label} » doit contenir au moins {min} caractères.", out error);
                if (rules?.MaxLength is int max && s.Length > max)
                    return Fail(field, $"« {field.Label} » ne doit pas dépasser {max} caractères.", out error);
                if (!string.IsNullOrWhiteSpace(rules?.Regex) && !SafeRegexMatch(rules!.Regex!, s))
                    return Fail(field, $"« {field.Label} » a un format invalide.", out error);
                return JsonValue.Create(s);
            }

            case CustomFieldType.Number:
            {
                if (!TryGetNumber(raw, out var num))
                    return Fail(field, $"« {field.Label} » doit être un nombre entier.", out error);
                if (num != Math.Truncate(num))
                    return Fail(field, $"« {field.Label} » doit être un nombre entier.", out error);
                if (!CheckRange(field, num, rules, out error)) return null;
                return JsonValue.Create((long)num);
            }

            case CustomFieldType.Decimal:
            {
                if (!TryGetNumber(raw, out var num))
                    return Fail(field, $"« {field.Label} » doit être un nombre.", out error);
                if (!CheckRange(field, num, rules, out error)) return null;
                return JsonValue.Create(num);
            }

            case CustomFieldType.Boolean:
            {
                if (raw is JsonValue v && v.TryGetValue<bool>(out var b))
                    return JsonValue.Create(b);
                return Fail(field, $"« {field.Label} » doit être vrai ou faux.", out error);
            }

            case CustomFieldType.Date:
            case CustomFieldType.DateTime:
            {
                var s = AsString(raw);
                if (string.IsNullOrEmpty(s)) return null;
                if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                    return Fail(field, $"« {field.Label} » doit être une date valide.", out error);
                return JsonValue.Create(s);
            }

            case CustomFieldType.Select:
            {
                var s = AsString(raw);
                if (string.IsNullOrEmpty(s)) return null;
                var options = StudioFieldJson.ParseOptions(field.OptionsJson) ?? Array.Empty<SelectOptionDto>();
                if (!options.Any(o => string.Equals(o.Value, s, StringComparison.Ordinal)))
                    return Fail(field, $"« {field.Label} » : valeur non autorisée.", out error);
                return JsonValue.Create(s);
            }

            case CustomFieldType.MultiSelect:
            {
                if (raw is not JsonArray arr)
                    return Fail(field, $"« {field.Label} » doit être une liste.", out error);
                var options = StudioFieldJson.ParseOptions(field.OptionsJson) ?? Array.Empty<SelectOptionDto>();
                var allowed = options.Select(o => o.Value).ToHashSet(StringComparer.Ordinal);
                var outArr = new JsonArray();
                foreach (var item in arr)
                {
                    var s = AsString(item);
                    if (s is null || !allowed.Contains(s))
                        return Fail(field, $"« {field.Label} » : valeur non autorisée.", out error);
                    outArr.Add(JsonValue.Create(s));
                }
                return outArr;
            }

            case CustomFieldType.RelationCustom:
            {
                var s = AsString(raw);
                if (string.IsNullOrEmpty(s)) return null;
                // MVP: validate Guid shape. Existence check is added in the relations phase.
                if (!Guid.TryParse(s, out _))
                    return Fail(field, $"« {field.Label} » : référence invalide.", out error);
                return JsonValue.Create(s);
            }

            case CustomFieldType.RelationExisting:
            {
                var s = AsString(raw);
                if (string.IsNullOrEmpty(s)) return null;
                return JsonValue.Create(s);
            }

            case CustomFieldType.Money:
            case CustomFieldType.Percentage:
            {
                if (!TryGetNumber(raw, out var num))
                    return Fail(field, $"« {field.Label} » doit être un nombre.", out error);
                if (!CheckRange(field, num, rules, out error)) return null;
                return JsonValue.Create(num);
            }

            case CustomFieldType.Rating:
            {
                if (!TryGetNumber(raw, out var num))
                    return Fail(field, $"« {field.Label} » doit être une note.", out error);
                var max = RatingMax(field.OptionsJson);
                var r = (long)Math.Round(num);
                if (r < 0 || r > max)
                    return Fail(field, $"« {field.Label} » doit être entre 0 et {max}.", out error);
                return JsonValue.Create(r);
            }

            case CustomFieldType.QrCode:
            case CustomFieldType.Barcode:
            {
                var s = AsString(raw);
                if (string.IsNullOrEmpty(s)) return null;
                if (rules?.MaxLength is int maxLen && s.Length > maxLen)
                    return Fail(field, $"« {field.Label} » ne doit pas dépasser {maxLen} caractères.", out error);
                return JsonValue.Create(s);
            }

            case CustomFieldType.Attachment:
            case CustomFieldType.Signature:
            {
                var s = AsString(raw);
                if (string.IsNullOrEmpty(s)) return null;
                if (s.Length > 1024)
                    return Fail(field, $"« {field.Label} » : référence de fichier trop longue.", out error);
                // Defense: only accept a relative URL produced by our own Studio file storage.
                if (!IsStudioFileUrl(s))
                    return Fail(field, $"« {field.Label} » : référence de fichier invalide.", out error);
                return JsonValue.Create(s);
            }

            default:
                return Fail(field, $"« {field.Label} » : type de champ non supporté.", out error);
        }
    }

    /// <summary>Server-derived field types whose value is never accepted from the client payload.</summary>
    public static bool IsComputed(CustomFieldType type) =>
        type is CustomFieldType.AutoNumber or CustomFieldType.Formula
            or CustomFieldType.Lookup or CustomFieldType.Rollup;

    /// <summary>A value stored by Attachment/Signature must be a relative URL under our own Studio file storage.</summary>
    public static bool IsStudioFileUrl(string s) =>
        s.StartsWith("/uploads/tenants/", StringComparison.Ordinal) && s.Contains("/studio/", StringComparison.Ordinal);

    private static JsonNode? Fail(CustomFieldDefinition field, string message, out Error? error)
    {
        error = Error.Validation(field.Key, message);
        return null;
    }

    private static int RatingMax(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return 5;
        try
        {
            if (JsonNode.Parse(optionsJson)?["rating"]?["max"] is JsonValue v && v.TryGetValue<int>(out var i) && i is >= 1 and <= 10)
                return i;
        }
        catch (JsonException) { }
        return 5;
    }

    private static bool CheckRange(CustomFieldDefinition field, decimal num, FieldValidationRules? rules, out Error? error)
    {
        error = null;
        if (rules?.Min is decimal min && num < min)
        {
            error = Error.Validation(field.Key, $"« {field.Label} » doit être ≥ {min}.");
            return false;
        }
        if (rules?.Max is decimal max && num > max)
        {
            error = Error.Validation(field.Key, $"« {field.Label} » doit être ≤ {max}.");
            return false;
        }
        return true;
    }

    private static string? AsString(JsonNode? node)
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            return v.ToString();
        }
        return null;
    }

    private static bool TryGetNumber(JsonNode node, out decimal value)
    {
        value = 0m;
        if (node is not JsonValue v)
            return false;

        // API-parsed input is JsonElement-backed; designer/test input may be CLR-typed.
        if (v.TryGetValue<JsonElement>(out var el))
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var de)) { value = de; return true; }
            if (el.ValueKind == JsonValueKind.String &&
                decimal.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var se)) { value = se; return true; }
            return false;
        }

        if (v.TryGetValue<decimal>(out var d)) { value = d; return true; }
        if (v.TryGetValue<double>(out var db)) { value = (decimal)db; return true; }
        if (v.TryGetValue<long>(out var l)) { value = l; return true; }
        if (v.TryGetValue<int>(out var i)) { value = i; return true; }
        if (v.TryGetValue<string>(out var s) &&
            decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var ds)) { value = ds; return true; }
        return false;
    }

    private static bool SafeRegexMatch(string pattern, string input)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (RegexMatchTimeoutException) { return false; }
        catch (ArgumentException) { return true; } // invalid pattern → don't block the user's data
    }
}
