using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// In-app notifications (master database). A notification targets a tenant and,
/// optionally, a role within that tenant or a single user. Consumers only see
/// notifications whose user restriction matches them, or whose role restriction
/// is absent or matches their own role (when not user-targeted).
/// </summary>
public interface INotificationService
{
    Task<Result> CreateAsync(
        Guid recipientTenantId,
        string? recipientRole,
        NotificationType type,
        string title,
        string body,
        string? linkUrl = null,
        CancellationToken cancellationToken = default);

    Task<Result> CreateAsync(
        Guid recipientTenantId,
        string? recipientRole,
        NotificationType type,
        string title,
        string body,
        string? linkUrl,
        Guid? recipientUserId,
        CancellationToken cancellationToken);

    Task<NotificationListDto> GetListAsync(
        Guid tenantId, string? role, bool unreadOnly, int page, int pageSize,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid tenantId, string? role, Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<Result> MarkReadAsync(
        Guid tenantId, string? role, Guid notificationId, Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<Result> MarkAllReadAsync(
        Guid tenantId, string? role, Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
}
