using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.AI.DTOs;

namespace FactuTrust.Application.Features.AI;

/// <summary>Explose le catalogue SDK Cursor (variants) en lignes <see cref="UnifiedAiModelInfo"/>.</summary>
public static class CursorModelCatalog
{
    public static IReadOnlyList<UnifiedAiModelInfo> ToUnifiedModels(IEnumerable<CursorRemoteModelInfo> models)
    {
        var unified = new List<UnifiedAiModelInfo>();
        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Id))
                continue;

            if (model.Variants is { Count: > 0 })
            {
                foreach (var variant in model.Variants)
                {
                    var modelRef = ModelRef.FormatCursor(model.Id, variant.Params);
                    var label = string.IsNullOrWhiteSpace(variant.DisplayName)
                        ? model.DisplayName
                        : variant.DisplayName;
                    unified.Add(new UnifiedAiModelInfo(
                        modelRef,
                        "cursor",
                        label,
                        null,
                        null,
                        SupportsVision: true,
                        SupportsChat: true));
                }

                continue;
            }

            unified.Add(new UnifiedAiModelInfo(
                ModelRef.FormatCursor(model.Id),
                "cursor",
                string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName,
                null,
                null,
                SupportsVision: true,
                SupportsChat: true));
        }

        return unified;
    }
}
