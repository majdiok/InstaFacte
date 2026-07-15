using Microsoft.AspNetCore.Http;
using FactuTrust.Application.Common.Interfaces;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Keys used to store tenant info in HttpContext.Items (request-scoped, avoids AsyncLocal loss).
/// </summary>
internal static class TenantContextKeys
{
    public const string TenantId = "TenantId";
    public const string ConnectionString = "TenantConnectionString";
}

/// <summary>
/// Provides access to the current tenant context.
/// Uses HttpContext.Items as primary source (request-scoped) and AsyncLocal as fallback.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<TenantInfo?> _currentTenant = new();
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId => GetFromHttpContext() ?? _currentTenant.Value?.TenantId;

    public string? ConnectionString => GetConnectionStringFromHttpContext() ?? _currentTenant.Value?.ConnectionString;

    public void SetTenant(Guid tenantId, string connectionString)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items[TenantContextKeys.TenantId] = tenantId;
            httpContext.Items[TenantContextKeys.ConnectionString] = connectionString;
        }
        _currentTenant.Value = new TenantInfo(tenantId, connectionString);
    }

    public void Clear()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items.Remove(TenantContextKeys.TenantId);
            httpContext.Items.Remove(TenantContextKeys.ConnectionString);
        }
        _currentTenant.Value = null;
    }

    private Guid? GetFromHttpContext()
    {
        var value = _httpContextAccessor.HttpContext?.Items[TenantContextKeys.TenantId];
        return value is Guid g ? g : null;
    }

    private string? GetConnectionStringFromHttpContext()
    {
        return _httpContextAccessor.HttpContext?.Items[TenantContextKeys.ConnectionString] as string;
    }

    private sealed record TenantInfo(Guid TenantId, string ConnectionString);
}
