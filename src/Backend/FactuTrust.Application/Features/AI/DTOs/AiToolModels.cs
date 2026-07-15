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

    public static AiToolResult Ok(string data) => new() { Success = true, Data = data };
    public static AiToolResult Error(string message) => new() { Success = false, ErrorMessage = message };
}
