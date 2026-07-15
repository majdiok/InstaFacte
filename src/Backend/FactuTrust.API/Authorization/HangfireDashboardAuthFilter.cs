using FactuTrust.Domain.Auth;
using Hangfire.Dashboard;

namespace FactuTrust.API.Authorization;

/// <summary>
/// Lot B5 — Filtre d'autorisation pour le dashboard Hangfire <c>/hangfire</c>.
///
/// Politique :
/// <list type="bullet">
///   <item><b>Production</b> : exige une session authentifiée avec le rôle <see cref="PlatformRoles.PlatformAdmin"/>
///         (super-admin uniquement). Le token doit être présent dans le header <c>Authorization: Bearer</c>.</item>
///   <item><b>Développement / Test</b> : accepte toute requête depuis localhost/127.0.0.1 sans authentification
///         (pour faciliter le debug). Toute autre IP → 401.</item>
/// </list>
///
/// <b>Limitation connue</b> : le dashboard Hangfire est servi en HTML via le navigateur, qui n'envoie pas
/// le header <c>Authorization</c> automatiquement. En prod, l'accès se fait via un reverse-proxy auth basique
/// ou un client capable d'injecter le header (curl, ModHeader, Postman). Une amélioration future pourrait
/// introduire une auth cookie dédiée pour le dashboard.
/// </summary>
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    private readonly IWebHostEnvironment _environment;

    public HangfireDashboardAuthFilter(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // En dev : tolérant sur localhost (pour faciliter le debug local).
        if (_environment.IsDevelopment())
        {
            var remoteIp = httpContext.Connection.RemoteIpAddress;
            if (remoteIp is not null
                && (System.Net.IPAddress.IsLoopback(remoteIp) || remoteIp.ToString() is "127.0.0.1" or "::1"))
            {
                return true;
            }
        }

        // En prod : rôle PlatformAdmin uniquement.
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(PlatformRoles.PlatformAdmin);
    }
}
