using FactuTrust.Application.Configuration;

namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Determines whether an AI tool may run in the parallel pre-execution pass (Pass A).
/// </summary>
public static class AiToolParallelEligibility
{
    public static bool IsEligible(string toolName, OllamaSettings settings)
    {
        if (IsParallelSafeNoDbTool(toolName))
            return settings.EnableParallelToolCalls;

        if (AiParallelDbToolPolicy.IsSafe(toolName))
            return settings.EnableParallelDbTools;

        return false;
    }

    public static bool IsParallelSafeNoDbTool(string toolName) => toolName switch
    {
        "resolve_reporting_period" => true,
        "get_tunisian_commercial_calendar" => true,
        "generate_dashboard_config" => true,
        "propose_client_actions" => true,
        "propose_follow_up_prompts" => true,
        _ => false
    };
}