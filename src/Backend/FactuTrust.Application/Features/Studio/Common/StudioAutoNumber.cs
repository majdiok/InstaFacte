using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Pure formatting for AutoNumber fields: turns a reserved sequence value into its display reference
/// using the field's <c>number.{prefix,padding,suffix}</c> config. No I/O — unit-testable in isolation.
/// </summary>
public static class StudioAutoNumber
{
    public const int MaxPadding = 12;
    public const int MaxAffixLength = 16;

    public static string Format(long sequence, string? optionsJson)
    {
        var (prefix, padding, suffix) = ParseConfig(optionsJson);
        var num = sequence.ToString(CultureInfo.InvariantCulture);
        if (padding > 0) num = num.PadLeft(padding, '0');
        return $"{prefix}{num}{suffix}";
    }

    /// <summary>Reads (prefix, padding, suffix) from OptionsJson, clamped to safe bounds with sane defaults.</summary>
    public static (string Prefix, int Padding, string Suffix) ParseConfig(string? optionsJson)
    {
        var prefix = string.Empty;
        var suffix = string.Empty;
        var padding = 4;

        if (!string.IsNullOrWhiteSpace(optionsJson))
        {
            try
            {
                if (JsonNode.Parse(optionsJson)?["number"] is JsonObject number)
                {
                    if (number["prefix"] is JsonValue p && p.TryGetValue<string>(out var ps)) prefix = ps;
                    if (number["suffix"] is JsonValue s && s.TryGetValue<string>(out var ss)) suffix = ss;
                    if (number["padding"] is JsonValue d && d.TryGetValue<int>(out var di)) padding = di;
                }
            }
            catch (JsonException) { /* fall back to defaults on corrupt config */ }
        }

        if (prefix.Length > MaxAffixLength) prefix = prefix[..MaxAffixLength];
        if (suffix.Length > MaxAffixLength) suffix = suffix[..MaxAffixLength];
        padding = Math.Clamp(padding, 0, MaxPadding);
        return (prefix, padding, suffix);
    }
}
