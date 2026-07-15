using System.Security.Claims;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;

namespace FactuTrust.API.Middleware;

/// <summary>
/// Blocks mutating requests in delegated firm context except accounting and fiscal endpoints.
/// </summary>
public sealed class DelegatedAccessMiddleware
{
    private static readonly HashSet<string> AllowedWritePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/accounting",
        "/api/bank-reconciliation",
        "/api/withholding-tax",
        // Payroll (RH & Paie) is a delegated-firm responsibility: the accounting firm must be able
        // to manage employees, run payroll and file social declarations on behalf of client dossiers.
        "/api/payroll"
    };

    /// <summary>
    /// Session and firm-context management allowed in delegated mode (exit/switch dossier, token refresh).
    /// </summary>
    private static readonly HashSet<string> AllowedDelegatedSessionPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/firm/context",
        "/api/auth/refresh",
        "/api/auth/logout"
    };

    private static readonly HashSet<string> BlockedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/tenant-users",
        "/api/firm/users",
        "/api/studio",
        "/api/storefront",
        "/api/pos"
    };

    /// <summary>
    /// GET/HEAD blocked for delegated firm users outside sales/purchases allowlist.
    /// </summary>
    private static readonly HashSet<string> DelegatedBlockedReadPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/quotes",
        "/api/deliverynotes",
        "/api/suppliers",
        "/api/purchaseorders"
    };

    private readonly RequestDelegate _next;

    public DelegatedAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var accessMode = context.User.FindFirstValue(AuthClaimTypes.AccessMode);
        if (!string.Equals(accessMode, "delegated", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";

        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            if (DelegatedBlockedReadPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await WriteForbiddenAsync(context, "Consultation interdite en mode dossier client (hors périmètre autorisé).");
                return;
            }

            await _next(context);
            return;
        }

        foreach (var blocked in BlockedPrefixes)
        {
            if (path.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
            {
                await WriteForbiddenAsync(context, "Action interdite en mode dossier client délégué.");
                return;
            }
        }

        if (AllowedDelegatedSessionPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var allowed = AllowedWritePrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            await WriteForbiddenAsync(context, "Modification interdite en mode dossier client (lecture seule hors comptabilité/fiscal).");
            return;
        }

        await _next(context);
    }

    private static async Task WriteForbiddenAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        var response = ApiResponse<object>.Fail(message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
