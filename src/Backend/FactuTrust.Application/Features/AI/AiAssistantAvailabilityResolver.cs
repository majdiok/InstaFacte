namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Règles partagées pour /api/ai/health et le badge « disponible » côté assistant.
/// </summary>
public static class AiAssistantAvailabilityResolver
{
    public static bool HasCloudProviderConfigured(
        bool openRouterEnabled,
        string? openRouterApiKey,
        bool cursorSdkEnabled,
        bool cursorDbEnabled,
        string? cursorApiKey) =>
        (openRouterEnabled && !string.IsNullOrEmpty(openRouterApiKey))
        || (cursorSdkEnabled && cursorDbEnabled && !string.IsNullOrEmpty(cursorApiKey));

    /// <summary>
    /// Vrai si l'assistant peut raisonnablement être considéré disponible (badge UI).
    /// Quand le modèle actif est Cursor, le pont local doit répondre.
    /// </summary>
    public static async Task<bool> IsHealthAvailableAsync(
        bool ollamaOk,
        bool hasCloudProvider,
        ParsedModelRef activeModel,
        Func<CancellationToken, Task<bool>> cursorBridgeProbe,
        CancellationToken cancellationToken = default)
    {
        if (ollamaOk)
            return true;

        if (!hasCloudProvider)
            return false;

        if (activeModel.Kind == LlmProviderKind.Cursor)
            return await cursorBridgeProbe(cancellationToken);

        return true;
    }
}
