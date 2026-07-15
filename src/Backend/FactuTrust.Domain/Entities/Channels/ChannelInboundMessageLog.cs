using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Channels;

/// <summary>
/// Idempotency record for processed inbound channel messages (unique per channel + external message id).
/// </summary>
public sealed class ChannelInboundMessageLog : Entity
{
    public ChannelType ChannelType { get; private set; }
    public string ExternalMessageId { get; private set; } = string.Empty;
    public string ExternalUserId { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public string TraceId { get; private set; } = string.Empty;

    private ChannelInboundMessageLog()
    {
    }

    public static ChannelInboundMessageLog Create(
        ChannelType channelType,
        string externalMessageId,
        string externalUserId,
        Guid userId,
        string traceId)
    {
        return new ChannelInboundMessageLog
        {
            ChannelType = channelType,
            ExternalMessageId = externalMessageId.Trim(),
            ExternalUserId = externalUserId.Trim(),
            UserId = userId,
            TraceId = traceId.Trim()
        };
    }
}
