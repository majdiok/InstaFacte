namespace FactuTrust.Application.Configuration;

/// <summary>Sidecar Node @cursor/sdk. Désactivé par défaut : aucun process Node, aucun appel réseau.</summary>
public sealed class CursorSdkSettings
{
    public const string SectionName = "CursorSdk";

    /// <summary>Master switch. When false the sidecar is not started.</summary>
    public bool Enabled { get; set; }

    /// <summary>Node 22.13+ executable. Distinct from Channels:NodeExecutablePath.</summary>
    public string NodeExecutablePath { get; set; } = "node";

    /// <summary>Optional override of the bridge directory (bridge.js + node_modules).</summary>
    public string? BridgeDirectory { get; set; }

    /// <summary>Loopback port. 0 = ephemeral, announced on the READY line.</summary>
    public int BridgePort { get; set; }

    /// <summary>
    /// Base URL the sidecar uses to call back into this API for custom tools
    /// (e.g. http://127.0.0.1:7000). Empty = http://127.0.0.1:7000.
    /// </summary>
    public string ToolCallbackBaseUrl { get; set; } = "http://127.0.0.1:7000";

    public int RunTimeoutSeconds { get; set; } = 180;

    public int ExtractTimeoutSeconds { get; set; } = 120;

    public int ModelsCacheSeconds { get; set; } = 60;

    public int StartupTimeoutSeconds { get; set; } = 20;
}
