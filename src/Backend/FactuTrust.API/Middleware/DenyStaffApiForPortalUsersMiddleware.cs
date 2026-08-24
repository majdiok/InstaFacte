using System.Text.Encodings.Web;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.ClientPortal;

namespace FactuTrust.API.Middleware;

/// <summary>
/// Portal JWTs may only call auth, portal, and anonymous public/health APIs.
/// Staff ERP endpoints return a generic 403 (no resource existence leak).
/// </summary>
public sealed class DenyStaffApiForPortalUsersMiddleware
{
    private readonly RequestDelegate _next;

    public DenyStaffApiForPortalUsersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        if (currentUser.IsAuthenticated
            && currentUser.IsClientPortal
            && ClientPortalAccess.IsDeniedStaffApiPath(context.Request.Path.Value ?? string.Empty))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = ApiResponse<object>.Fail("Accès refusé.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
            return;
        }

        await _next(context);
    }
}
