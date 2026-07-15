namespace FactuTrust.Application.Configuration;

public sealed class OpenRouterSettings
{
    public const string SectionName = "OpenRouter";

    /// <summary>Default API root including /v1 (e.g. https://openrouter.ai/api/v1).</summary>
    public string DefaultBaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public string? HttpReferer { get; set; }

    public string AppTitle { get; set; } = "FactuTrust";

    public int TimeoutSeconds { get; set; } = 120;

    public int ModelListCacheSeconds { get; set; } = 120;
}
