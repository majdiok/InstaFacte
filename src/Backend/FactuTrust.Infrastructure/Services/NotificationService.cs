using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private readonly MasterDbContext _masterContext;

    public NotificationService(MasterDbContext masterContext)
    {
        _masterContext = masterContext;
    }

    public async Task<Result> CreateAsync(
        Guid recipientTenantId,
        string? recipientRole,
        NotificationType type,
        string title,
        string body,
        string? linkUrl = null,
        CancellationToken cancellationToken = default)
    {
        var createResult = UserNotification.Create(recipientTenantId, recipientRole, type, title, body, linkUrl);
        if (createResult.IsFailure)
            return Result.Failure(createResult.Error);

        _masterContext.UserNotifications.Add(createResult.Value);
        await _masterContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<NotificationListDto> GetListAsync(
        Guid tenantId, string? role, bool unreadOnly, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = VisibleTo(tenantId, role);
        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await VisibleTo(tenantId, role).CountAsync(n => n.ReadAt == null, cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Body = n.Body,
                LinkUrl = n.LinkUrl,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            })
            .ToListAsync(cancellationToken);

        return new NotificationListDto
        {
            Items = items,
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<int> GetUnreadCountAsync(Guid tenantId, string? role, CancellationToken cancellationToken = default)
    {
        return await VisibleTo(tenantId, role).CountAsync(n => n.ReadAt == null, cancellationToken);
    }

    public async Task<Result> MarkReadAsync(Guid tenantId, string? role, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _masterContext.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientTenantId == tenantId &&
                (n.RecipientRole == null || n.RecipientRole == role), cancellationToken);

        if (notification is null)
            return Result.Failure(Error.NotFound("Notification", notificationId));

        notification.MarkRead();
        await _masterContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(Guid tenantId, string? role, CancellationToken cancellationToken = default)
    {
        var unread = await _masterContext.UserNotifications
            .Where(n => n.RecipientTenantId == tenantId &&
                (n.RecipientRole == null || n.RecipientRole == role) &&
                n.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
            notification.MarkRead();

        if (unread.Count > 0)
            await _masterContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private IQueryable<UserNotification> VisibleTo(Guid tenantId, string? role) =>
        _masterContext.UserNotifications.AsNoTracking()
            .Where(n => n.RecipientTenantId == tenantId &&
                (n.RecipientRole == null || n.RecipientRole == role));
}
