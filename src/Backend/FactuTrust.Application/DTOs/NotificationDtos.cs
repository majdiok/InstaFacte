using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record NotificationDto
{
    public Guid Id { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = null!;
    public string Body { get; init; } = null!;
    public string? LinkUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public sealed record NotificationListDto
{
    public IReadOnlyList<NotificationDto> Items { get; init; } = Array.Empty<NotificationDto>();
    public int TotalCount { get; init; }
    public int UnreadCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
