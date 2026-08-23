using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

/// <summary>User-facing errors when Modal credentials are missing, scoped to platform vs tenant override.</summary>
public static class ModalCredentialMessages
{
    public static string Unavailable(ResolvedModalCredentials? credentials = null)
    {
        if (credentials?.Source == ModalCredentialSource.Disabled)
        {
            return "Modal est désactivé pour cette entreprise. Activez-le dans le back-office > Entreprises > fiche > Configuration IA, ou revenez à la configuration plateforme.";
        }

        var tenantScoped = credentials?.Source is ModalCredentialSource.TenantOverride
            or ModalCredentialSource.Disabled;

        if (credentials is not null && string.IsNullOrWhiteSpace(credentials.BaseUrl))
        {
            return tenantScoped
                ? "Aucune URL Modal configurée. Renseignez-la dans le back-office > Entreprises > fiche > Configuration IA."
                : "Aucune URL Modal configurée. Renseignez-la dans le back-office plateforme > Configuration IA (Modal).";
        }

        return tenantScoped
            ? "Aucune clé API Modal configurée. Configurez-la dans le back-office > Entreprises > fiche > Configuration IA."
            : "Aucune clé API Modal configurée. Configurez-la dans le back-office plateforme > Configuration IA (Modal).";
    }
}
