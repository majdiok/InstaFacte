using System.Text.Json;

namespace FactuTrust.Application.Common.Fiscal;

/// <summary>Tranche du barème IRPP progressif : borne inférieure (TND) et taux marginal (en %).</summary>
public sealed record IrppBracket(decimal Lower, decimal Rate);

/// <summary>Parsing tolérant du barème IRPP sérialisé (JSON) stocké dans les paramètres d'exercice.</summary>
public static class IrppScale
{
    private sealed record RawBracket(decimal lower, decimal rate);

    public static IReadOnlyList<IrppBracket> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<IrppBracket>();

        try
        {
            var raw = JsonSerializer.Deserialize<List<RawBracket>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (raw is null)
                return Array.Empty<IrppBracket>();

            return raw
                .Select(b => new IrppBracket(b.lower, b.rate))
                .OrderBy(b => b.Lower)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<IrppBracket>();
        }
    }
}
