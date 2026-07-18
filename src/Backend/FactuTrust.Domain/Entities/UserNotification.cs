using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// In-app notification stored in the master database (cross-tenant flows such as
/// company ↔ accounting-firm assignments). Targeted at a tenant, optionally
/// narrowed to a role within that tenant; read state is shared per notification.
/// </summary>
public sealed class UserNotification : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 1000;
    public const int LinkUrlMaxLength = 300;
    public const int RecipientRoleMaxLength = 50;

    public Guid RecipientTenantId { get; private set; }
    public string? RecipientRole { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string? LinkUrl { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private UserNotification() { }

    public static Result<UserNotification> Create(
        Guid recipientTenantId,
        string? recipientRole,
        NotificationType type,
        string title,
        string body,
        string? linkUrl = null)
    {
        if (recipientTenantId == Guid.Empty)
            return Result.Failure<UserNotification>(Error.Validation("Recipient", "Tenant destinataire invalide"));

        var trimmedTitle = title?.Trim();
        if (string.IsNullOrEmpty(trimmedTitle))
            return Result.Failure<UserNotification>(Error.Validation("Title", "Le titre est obligatoire"));

        return Result.Success(new UserNotification
        {
            RecipientTenantId = recipientTenantId,
            RecipientRole = string.IsNullOrWhiteSpace(recipientRole) ? null : recipientRole.Trim(),
            Type = type,
            Title = Truncate(trimmedTitle, TitleMaxLength)!,
            Body = Truncate(body?.Trim(), BodyMaxLength) ?? string.Empty,
            LinkUrl = Truncate(linkUrl?.Trim(), LinkUrlMaxLength)
        });
    }

    public void MarkRead()
    {
        ReadAt ??= DateTime.UtcNow;
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
