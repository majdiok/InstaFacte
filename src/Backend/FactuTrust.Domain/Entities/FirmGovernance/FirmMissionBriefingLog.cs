using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.FirmGovernance;

/// <summary>
/// Trace d'envoi du brief quotidien « Chef de mission », une ligne par cabinet, destinataire et jour.
/// </summary>
/// <remarks>
/// Sert uniquement d'anti-doublon. Les jobs récurrents Hangfire peuvent se redéclencher (redémarrage
/// du serveur, reprise après incident) et le brief n'a pas d'équivalent du <c>LastReminderAt</c> que
/// les échéances fiscales portent déjà. L'unicité est garantie par un index unique sur
/// (FirmTenantId, RecipientUserId, BriefingDate) : c'est la base qui tranche, pas le job.
/// </remarks>
public sealed class FirmMissionBriefingLog : Entity
{
    public Guid FirmTenantId { get; private set; }
    public Guid RecipientUserId { get; private set; }

    /// <summary>Jour du brief (date seule), clé fonctionnelle de l'anti-doublon.</summary>
    public DateTime BriefingDate { get; private set; }

    public DateTime SentAt { get; private set; }

    public FiscalReminderChannel Channel { get; private set; } = FiscalReminderChannel.Email;

    /// <summary>Nombre d'échéances en retard au moment de l'envoi — utile pour auditer un brief a posteriori.</summary>
    public int OverdueCount { get; private set; }

    /// <summary>Vrai si le portefeuille n'a pas pu être lu en totalité lors de la génération.</summary>
    public bool PartialRead { get; private set; }

    private FirmMissionBriefingLog() { }

    public static FirmMissionBriefingLog Create(
        Guid firmTenantId,
        Guid recipientUserId,
        DateTime briefingDate,
        DateTime sentAt,
        int overdueCount,
        bool partialRead,
        FiscalReminderChannel channel = FiscalReminderChannel.Email) => new()
        {
            Id = Guid.NewGuid(),
            FirmTenantId = firmTenantId,
            RecipientUserId = recipientUserId,
            BriefingDate = briefingDate.Date,
            SentAt = sentAt,
            OverdueCount = overdueCount,
            PartialRead = partialRead,
            Channel = channel
        };
}
