namespace FactuTrust.Application.DTOs;

/// <summary>
/// Result of <c>ITenantService.ReSeedFiscalParametersAsync</c> (plan §2.4) — what the additive
/// re-seed actually touched, plus a non-blocking warning when the new regime is atypical for the
/// tenant's segment. Surfaced by <c>CompanyController.UpdateCompany</c> in its response so the
/// French UI can show "Paramètres de retenue à la source actualisés" only when something changed.
/// </summary>
public sealed record FiscalReSeedResultDto
{
    /// <summary>French, human-readable labels of the fiscal catalogs that were inserted/refreshed. Empty when everything was already up to date.</summary>
    public required IReadOnlyList<string> UpdatedItems { get; init; }

    /// <summary>Non-blocking French warning when the new regime is unusual for the tenant's segment, otherwise null.</summary>
    public string? Warning { get; init; }
}
