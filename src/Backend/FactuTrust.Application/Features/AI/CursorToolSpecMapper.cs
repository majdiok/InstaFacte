using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

public static class CursorToolSpecMapper
{
    public static IReadOnlyList<CursorToolSpec> FromOllamaTools(IReadOnlyList<OllamaToolDefinition> tools)
    {
        var specs = new List<CursorToolSpec>(tools.Count);
        foreach (var tool in tools)
        {
            if (tool.Function is null || string.IsNullOrWhiteSpace(tool.Function.Name))
                continue;

            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (name, prop) in tool.Function.Parameters.Properties)
            {
                var node = new Dictionary<string, object?>
                {
                    ["type"] = prop.Type,
                    ["description"] = prop.Description
                };
                if (prop.Enum is { Count: > 0 })
                    node["enum"] = prop.Enum;
                properties[name] = node;
            }

            var schema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = tool.Function.Parameters.Required
            };

            specs.Add(new CursorToolSpec(tool.Function.Name, tool.Function.Description, schema));
        }

        return specs;
    }

    public static IReadOnlyList<CursorImagePayload> FromAttachments(IReadOnlyList<ChatAttachmentInput>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
            return Array.Empty<CursorImagePayload>();

        return attachments
            .Where(a => a.ImagesBase64 is { Count: > 0 })
            .SelectMany(a => a.ImagesBase64!)
            .Where(b64 => !string.IsNullOrWhiteSpace(b64))
            .Take(10)
            .Select(b64 => new CursorImagePayload(b64, "image/png"))
            .ToList();
    }

    public static IReadOnlyList<CursorImagePayload> FromBase64List(IReadOnlyList<string> images) =>
        images
            .Where(b64 => !string.IsNullOrWhiteSpace(b64))
            .Take(10)
            .Select(b64 => new CursorImagePayload(b64, "image/png"))
            .ToList();
}
