using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.AI;

/// <summary>
/// Audit trail row for every AI assistant export the platform generates (PowerPoint, future formats).
/// Stored per tenant to support enterprise compliance, RGPD purging, billing and usage analytics.
/// </summary>
public class AiExportAudit : Entity
{
    public Guid UserId { get; private set; }
    public string Format { get; private set; } = "pptx";
    public string Template { get; private set; } = "Standard";
    public string? Title { get; private set; }
    public int ResponseCount { get; private set; }
    public int SlideCount { get; private set; }
    public long SizeBytes { get; private set; }
    public int DurationMs { get; private set; }
    public string? StoragePath { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public bool Success { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>JSON array of conversation GUIDs used as sources.</summary>
    public string ConversationIdsJson { get; private set; } = "[]";

    /// <summary>JSON array of message GUIDs included in the export.</summary>
    public string MessageIdsJson { get; private set; } = "[]";

    private AiExportAudit() { }

    public static AiExportAudit Create(
        Guid userId,
        string format,
        string template,
        string? title,
        int responseCount,
        int slideCount,
        long sizeBytes,
        int durationMs,
        string? storagePath,
        bool success,
        IEnumerable<Guid> conversationIds,
        IEnumerable<Guid> messageIds,
        string? failureReason = null)
    {
        return new AiExportAudit
        {
            UserId = userId,
            Format = format,
            Template = template,
            Title = title,
            ResponseCount = responseCount,
            SlideCount = slideCount,
            SizeBytes = sizeBytes,
            DurationMs = durationMs,
            StoragePath = storagePath,
            GeneratedAt = DateTime.UtcNow,
            Success = success,
            FailureReason = failureReason,
            ConversationIdsJson = SerializeIds(conversationIds),
            MessageIdsJson = SerializeIds(messageIds)
        };
    }

    private static string SerializeIds(IEnumerable<Guid> ids)
    {
        var distinct = ids.Distinct().Select(id => $"\"{id:D}\"").ToArray();
        return distinct.Length == 0 ? "[]" : $"[{string.Join(",", distinct)}]";
    }
}
