namespace FactuTrust.Application.Features.Studio.Ai;

/// <summary>
/// États calculés par MediatR (soldes, encours commercial…) — hors moteur SQL du Studio.
/// Le routeur d'intention et les outils <c>studio_*_report</c> les reconnaissent par clé ;
/// ils ne figurent pas dans <see cref="Common.SqlReport.SqlReportPresetCatalog"/>.
/// </summary>
public sealed record StudioDeterministicReport(
    string Key,
    string DisplayName,
    string Description);

public static class StudioDeterministicReportCatalog
{
    public const string SoldesClients = "soldes_clients";
    public const string EncoursCommercialClients = "encours_commercial_clients";

    public static IReadOnlyList<StudioDeterministicReport> All { get; } = new[]
    {
        new StudioDeterministicReport(
            SoldesClients,
            "Soldes clients (balance âgée)",
            "Total facturé, encaissé, solde et ventilation par ancienneté d'échéance, par client."),
        new StudioDeterministicReport(
            EncoursCommercialClients,
            "Encours commercial par client",
            "Factures impayées + commandes confirmées non facturées, avec plafond et dépassement.")
    };

    public static StudioDeterministicReport? Find(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : All.FirstOrDefault(p => string.Equals(p.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsDeterministic(string? key) => Find(key) is not null;
}
