using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Exchange;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Routes Échanges in-app notifications to the opposite party.
/// Company actor → assigned collaborator if active, else all firm managers.
/// Firm actor → company administrators.
/// </summary>
public sealed class ExchangeOppositePartyNotifier : IExchangeOppositePartyNotifier
{
    private readonly MasterDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<ExchangeOppositePartyNotifier> _logger;

    public ExchangeOppositePartyNotifier(
        MasterDbContext db,
        INotificationService notifications,
        ILogger<ExchangeOppositePartyNotifier> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ExchangeNotificationTarget> NotifyAsync(
        ExchangeThread thread,
        Guid actorTenantId,
        NotificationType type,
        string title,
        string body,
        string tab,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveAsync(thread, actorTenantId, cancellationToken);
        var forCompany = target.RecipientTenantId == thread.CompanyTenantId;
        var link = BuildLink(forCompany, thread.Id, tab);

        try
        {
            if (target.RecipientUserId is { } uid && uid != Guid.Empty)
            {
                await _notifications.CreateAsync(
                    target.RecipientTenantId, target.RecipientRole, type, title, body, link, uid, cancellationToken);
            }
            else
            {
                await _notifications.CreateAsync(
                    target.RecipientTenantId, target.RecipientRole, type, title, body, link, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec notification exchange {Type}", type);
        }

        return target;
    }

    internal async Task<ExchangeNotificationTarget> ResolveAsync(
        ExchangeThread thread,
        Guid actorTenantId,
        CancellationToken cancellationToken)
    {
        if (actorTenantId == thread.CompanyTenantId)
        {
            var assignedUserId = await _db.PermanentFiles.AsNoTracking()
                .Where(p => p.FirmClientAssignmentId == thread.FirmClientAssignmentId
                            && p.FirmTenantId == thread.FirmTenantId)
                .Select(p => p.AssignedAccountantUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (assignedUserId is { } collabId && collabId != Guid.Empty)
            {
                var active = await _db.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == collabId
                                   && u.TenantId == thread.FirmTenantId
                                   && u.IsActive, cancellationToken);
                if (active)
                    return new ExchangeNotificationTarget(thread.FirmTenantId, null, collabId);
            }

            return new ExchangeNotificationTarget(thread.FirmTenantId, nameof(UserRole.FirmManager), null);
        }

        return new ExchangeNotificationTarget(thread.CompanyTenantId, nameof(UserRole.Administrator), null);
    }

    internal static string BuildLink(bool forCompany, Guid threadId, string tab)
    {
        var path = forCompany ? $"/exchanges/{threadId}" : $"/firm/exchanges/{threadId}";
        return string.IsNullOrWhiteSpace(tab) ? path : $"{path}?tab={tab.Trim()}";
    }
}
