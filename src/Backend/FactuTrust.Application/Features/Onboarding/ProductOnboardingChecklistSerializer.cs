using System.Text.Json;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.ProductOnboarding;

namespace FactuTrust.Application.Features.Onboarding;

public static class ProductOnboardingChecklistSerializer
{
    private const int MaxDoneIds = 32;
    private const int MaxIdLength = 64;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static ProductOnboardingChecklistDto Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ProductOnboardingChecklistDto();

        try
        {
            var parsed = JsonSerializer.Deserialize<ChecklistPayload>(json, JsonOptions);
            if (parsed is null)
                return new ProductOnboardingChecklistDto();

            return new ProductOnboardingChecklistDto
            {
                Dismissed = parsed.Dismissed,
                DoneIds = SanitizeIds(parsed.DoneIds)
            };
        }
        catch (JsonException)
        {
            return new ProductOnboardingChecklistDto();
        }
    }

    public static string Serialize(ProductOnboardingChecklistDto dto)
    {
        var payload = new ChecklistPayload
        {
            Dismissed = dto.Dismissed,
            DoneIds = SanitizeIds(dto.DoneIds).ToList()
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static ProductOnboardingChecklistDto WithDoneId(ProductOnboardingChecklistDto current, string? rawId)
    {
        var id = rawId?.Trim();
        if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength)
            return current;

        var done = new List<string>(SanitizeIds(current.DoneIds));
        if (!done.Contains(id, StringComparer.Ordinal))
            done.Add(id);

        return current with { DoneIds = done };
    }

    public static ProductOnboardingChecklistDto Empty() =>
        Deserialize(ProductOnboardingDefaults.EmptyChecklistJson);

    private static IReadOnlyList<string> SanitizeIds(IEnumerable<string>? input)
    {
        if (input is null) return Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var raw in input)
        {
            var id = raw?.Trim();
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength) continue;
            if (!seen.Add(id)) continue;
            result.Add(id);
            if (result.Count >= MaxDoneIds) break;
        }

        return result;
    }

    private sealed class ChecklistPayload
    {
        public bool Dismissed { get; set; }
        public List<string>? DoneIds { get; set; }
    }
}
