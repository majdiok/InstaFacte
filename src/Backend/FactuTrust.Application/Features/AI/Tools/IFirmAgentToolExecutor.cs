using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI.Tools;

/// <summary>
/// Exécuteur des outils de l'agent « Chef de mission ». Isolé de <c>AiToolExecutor</c> parce que
/// ces outils sont les seuls à travailler au périmètre du CABINET (plusieurs dossiers) et non du
/// dossier courant : ils n'ont aucune dépendance commune avec le reste du catalogue.
/// </summary>
public interface IFirmAgentToolExecutor
{
    Task<AiToolResult> ExecuteAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
}

/// <summary>Noms des outils au périmètre cabinet. Source unique pour le routage du dispatch.</summary>
public static class FirmAgentTools
{
    public const string PortfolioOverview = "get_firm_portfolio_overview";
    public const string FiscalDeadlines = "get_firm_fiscal_deadlines";
    public const string DossierHealth = "get_firm_dossier_health";
    public const string CollaboratorWorkload = "get_firm_collaborator_workload";
    public const string SendReminder = "send_fiscal_deadline_reminder";

    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        PortfolioOverview, FiscalDeadlines, DossierHealth, CollaboratorWorkload, SendReminder
    };

    public static bool Contains(string toolName) => Names.Contains(toolName);

    public static IReadOnlyCollection<string> All => Names;
}
