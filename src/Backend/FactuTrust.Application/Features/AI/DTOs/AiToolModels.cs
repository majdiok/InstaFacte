namespace FactuTrust.Application.Features.AI.DTOs;

public sealed record AiToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Dictionary<string, AiToolParameter> Parameters { get; init; }
    public List<string> RequiredParameters { get; init; } = new();

    /// <summary>When true, tool performs writes; filtered out when mutation tools are disabled in Ollama settings.</summary>
    public bool IsMutating { get; init; }

    /// <summary>If set, the current user must have this permission (same values as API policies, e.g. products:create).</summary>
    public string? RequiredPermission { get; init; }
}

public sealed record AiToolParameter
{
    public required string Type { get; init; }
    public required string Description { get; init; }
    public List<string>? AllowedValues { get; init; }
}

public sealed record AiToolResult
{
    public bool Success { get; init; }
    public string Data { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Vrai si <see cref="Data"/> provient d'une lecture réellement exploitable (pas seulement
    /// tentée). Posé mécaniquement par les exécuteurs qui savent qualifier leurs données (ex.
    /// <c>FirmAgentToolExecutor</c> depuis <c>DossiersRead</c>/<c>DossiersFailed</c>) — jamais par
    /// analyse du texte. Par défaut <c>false</c> : les 83 outils tenant existants qui n'appellent
    /// que <see cref="Ok(string)"/> ne sont pas affectés (Lot 1.3 du plan v3).
    /// </summary>
    public bool GroundedData { get; init; }

    /// <summary>
    /// Vrai si la réponse est ancrée sur une lecture de données réussie ET exploitable
    /// (<see cref="Success"/> &amp;&amp; <see cref="GroundedData"/>). Raccourci pour le grounding gate
    /// firm (Lot 1.3) et les sites de comptage <c>firmGroundedReads</c>.
    /// </summary>
    public bool IsGrounded => Success && GroundedData;

    public static AiToolResult Ok(string data) => new() { Success = true, Data = data };

    public static AiToolResult Ok(string data, bool groundedData) =>
        new() { Success = true, Data = data, GroundedData = groundedData };

    public static AiToolResult Error(string message) => new() { Success = false, ErrorMessage = message };
}
