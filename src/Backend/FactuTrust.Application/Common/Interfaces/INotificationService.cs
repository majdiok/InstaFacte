using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// In-app notifications (master database). A notification targets a tenant and,
/// optionally, a role within that tenant; consumers only see notifications whose
/// role restriction is absent or matches their own role.
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

    Task<NotificationListDto> GetListAsync(
        Guid tenantId, string? role, bool unreadOnly, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid tenantId, string? role, CancellationToken cancellationToken = default);

    Task<Result> MarkReadAsync(Guid tenantId, string? role, Guid notificationId, CancellationToken cancellationToken = default);

    Task<Result> MarkAllReadAsync(Guid tenantId, string? role, CancellationToken cancellationToken = default);
}
