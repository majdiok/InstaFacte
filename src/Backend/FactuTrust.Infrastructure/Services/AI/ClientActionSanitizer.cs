using System.Text.Json;
using System.Text.RegularExpressions;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>Validates LLM-proposed in-app navigation actions before exposing them to the client.</summary>
public static class ClientActionSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int MaxActions = 5;
    private const int MaxLabelLength = 120;
    private const int MaxQueryKeys = 12;

    private static readonly string[] AllowedRoutePrefixes =
    {
        "/dashboard",
        "/invoices",
        "/quotes",
        "/crm/",
        "/clients",
        "/products",
        "/product-categories",
        "/pos",
        "/accounting/",
        "/settings",
        "/ai-assistant",
        "/purchases",
        "/stock",
        "/inventory",
        "/withholding-tax",
        "/audit",
        "/delivery-notes",
        "/supplier-invoices",
        "/suppliers",
        "/purchase-orders",
        "/reports",
        "/payments",
        "/transfers"
    };

    private static readonly Regex SafeRouteSegment = new(
        @"^/[a-zA-Z0-9/_\-\.{}]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Validates a single in-app navigation link (same rules as <see cref="SanitizeActionsJson"/> without label).</summary>
    public static AiToolResult SanitizeNavigationLinkJson(string? linkJson)
    {
        if (string.IsNullOrWhiteSpace(linkJson))
            return AiToolResult.Error("link_json vide.");

        try
        {
            using var doc = JsonDocument.Parse(linkJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return AiToolResult.Error("link_json doit être un objet.");

            var el = doc.RootElement;
            if (!el.TryGetProperty("route", out var routeEl) || routeEl.ValueKind != JsonValueKind.String)
                return AiToolResult.Error("Propriété route (string) requise.");
            var route = NormalizeRoute(routeEl.GetString());
            if (route is null)
                return AiToolResult.Error("Route non autorisée ou invalide.");

            Dictionary<string, string>? query = null;
            if (el.TryGetProperty("queryParams", out var qp) && qp.ValueKind == JsonValueKind.Object)
            {
                query = new Dictionary<string, string>(StringComparer.Ordinal);
                var kc = 0;
                foreach (var p in qp.EnumerateObject())
                {
                    kc++;
                    if (kc > MaxQueryKeys)
                        return AiToolResult.Error("Trop de queryParams.");
                    if (!IsSafeQueryKey(p.Name))
                        return AiToolResult.Error("Clé query non autorisée.");
                    if (p.Value.ValueKind != JsonValueKind.String && p.Value.ValueKind != JsonValueKind.Number)
                        return AiToolResult.Error("Valeurs query en string ou nombre uniquement.");
                    var vs = p.Value.ValueKind == JsonValueKind.Number
                        ? p.Value.GetRawText()
                        : p.Value.GetString() ?? "";
                    if (vs.Length > 512)
                        return AiToolResult.Error("Valeur query trop longue.");
                    query[p.Name] = vs;
                }
            }

            var payload = new ClientActionDto("", route, query);
            var json = JsonSerializer.Serialize(
                new { route = payload.Route, queryParams = payload.QueryParams },
                JsonOptions);
            return AiToolResult.Ok(json);
        }
        catch (JsonException ex)
        {
            return AiToolResult.Error($"JSON invalide : {ex.Message}");
        }
    }

    /// <summary>Returns JSON array string for tool result and SSE, or error.</summary>
    public static AiToolResult SanitizeActionsJson(string? actionsJson)
    {
        if (string.IsNullOrWhiteSpace(actionsJson))
            return AiToolResult.Error("actions_json vide.");

        List<ClientActionDto> output;
        try
        {
            using var doc = JsonDocument.Parse(actionsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return AiToolResult.Error("actions_json doit être un tableau JSON.");

            var arr = doc.RootElement;
            if (arr.GetArrayLength() > MaxActions)
                return AiToolResult.Error($"Maximum {MaxActions} actions.");

            output = new List<ClientActionDto>();
            var i = 0;
            foreach (var el in arr.EnumerateArray())
            {
                i++;
                if (el.ValueKind != JsonValueKind.Object)
                    return AiToolResult.Error($"Action #{i} : objet attendu.");

                if (!el.TryGetProperty("label", out var labelEl) || labelEl.ValueKind != JsonValueKind.String)
                    return AiToolResult.Error($"Action #{i} : propriété label (string) requise.");
                var label = labelEl.GetString()?.Trim() ?? "";
                if (label.Length == 0 || label.Length > MaxLabelLength)
                    return AiToolResult.Error($"Action #{i} : label invalide.");

                if (!el.TryGetProperty("route", out var routeEl) || routeEl.ValueKind != JsonValueKind.String)
                    return AiToolResult.Error($"Action #{i} : propriété route (string) requise.");
                var route = NormalizeRoute(routeEl.GetString());
                if (route is null)
                    return AiToolResult.Error($"Action #{i} : route non autorisée ou invalide.");

                Dictionary<string, string>? query = null;
                if (el.TryGetProperty("queryParams", out var qp) && qp.ValueKind == JsonValueKind.Object)
                {
                    query = new Dictionary<string, string>(StringComparer.Ordinal);
                    var kc = 0;
                    foreach (var p in qp.EnumerateObject())
                    {
                        kc++;
                        if (kc > MaxQueryKeys)
                            return AiToolResult.Error($"Action #{i} : trop de queryParams.");
                        if (!IsSafeQueryKey(p.Name))
                            return AiToolResult.Error($"Action #{i} : clé query non autorisée.");
                        if (p.Value.ValueKind != JsonValueKind.String && p.Value.ValueKind != JsonValueKind.Number)
                            return AiToolResult.Error($"Action #{i} : valeurs query en string ou nombre uniquement.");
                        var vs = p.Value.ValueKind == JsonValueKind.Number
                            ? p.Value.GetRawText()
                            : p.Value.GetString() ?? "";
                        if (vs.Length > 512)
                            return AiToolResult.Error($"Action #{i} : valeur query trop longue.");
                        query[p.Name] = vs;
                    }
                }

                output.Add(new ClientActionDto(label, route, query));
            }

            if (output.Count == 0)
                return AiToolResult.Error("Aucune action valide.");
        }
        catch (JsonException ex)
        {
            return AiToolResult.Error($"JSON invalide : {ex.Message}");
        }

        var json = JsonSerializer.Serialize(output, JsonOptions);
        return AiToolResult.Ok(json);
    }

    private static string? NormalizeRoute(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var route = raw.Trim();
        if (route.Contains("://", StringComparison.Ordinal) ||
            route.Contains("..", StringComparison.Ordinal) ||
            route.Contains("<", StringComparison.Ordinal) ||
            route.Contains(">", StringComparison.Ordinal))
            return null;
        if (!route.StartsWith("/", StringComparison.Ordinal))
            route = "/" + route;
        if (!SafeRouteSegment.IsMatch(route.Split('?')[0]))
            return null;

        var pathOnly = route.Split('?')[0];
        if (!AllowedRoutePrefixes.Any(prefix =>
                pathOnly.Equals(prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
                pathOnly.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return null;

        return pathOnly;
    }

    private static bool IsSafeQueryKey(string name) =>
        name.Length > 0 && name.Length <= 64 && name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-');
}

internal sealed record ClientActionDto(string Label, string Route, Dictionary<string, string>? QueryParams);
