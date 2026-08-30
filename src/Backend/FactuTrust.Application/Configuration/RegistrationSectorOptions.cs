namespace FactuTrust.Application.Configuration;

/// <summary>
/// Sector-aware registration wizard kill-switch (plan §3 C6, §6.1 B3). When <see cref="Enabled"/>
/// is false, <c>POST /api/auth/register</c> ignores <c>CompanySegment</c>/<c>BusinessDomain</c>/
/// <c>EnabledModules</c> (logged, treated as absent) and <c>GET /api/public/sector-catalog</c>
/// returns 404. Both effects revert instantly by flipping this flag back — no redeploy, no data
/// migration.
/// </summary>
public sealed class RegistrationSectorOptions
{
    public const string SectionName = "Features:RegistrationSector";

    public bool Enabled { get; set; } = true;
}
