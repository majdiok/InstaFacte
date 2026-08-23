namespace FactuTrust.Application.Configuration;

public sealed class ModalSettings
{
    public const string SectionName = "Modal";

    /// <summary>Default API root including /v1. Empty = the back-office must supply the URL.</summary>
    public string DefaultBaseUrl { get; set; } = string.Empty;

    public string DefaultModelId { get; set; } = "moonshotai/Kimi-K3";

    public int TimeoutSeconds { get; set; } = 300;

    public int ColdStartRetries { get; set; } = 4;

    public string ReasoningEffort { get; set; } = "none";

    public bool EnableTools { get; set; } = true;

    public bool EnableStickySessions { get; set; } = true;
}
