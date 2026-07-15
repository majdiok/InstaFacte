using FactuTrust.Domain.Entities.Channels;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Channels;

/// <summary>
/// Invariants domaine des entités de canal : Rebind (réactivation + remplacement des identifiants
/// externes, imposé par les index uniques), cycle de vie des codes de liaison (tentatives,
/// consommation) et des routes master.
/// </summary>
public sealed class ChannelDomainTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);

    // ── ChannelIdentityLink ──

    [Fact]
    public void IdentityLink_Create_IsActive_WithTrimmedIds()
    {
        var link = ChannelIdentityLink.Create(
            Guid.NewGuid(), ChannelType.WhatsApp, " 21612345678@c.us ", " 21612345678@c.us ", Now);

        Assert.True(link.IsActive);
        Assert.Equal("21612345678@c.us", link.ExternalUserId);
        Assert.Equal("21612345678@c.us", link.ExternalChatId);
        Assert.Equal(Now, link.VerifiedAt);
    }

    [Fact]
    public void IdentityLink_Rebind_ReactivatesAndReplacesExternalIds()
    {
        // Piège concret : l'index unique (UserId, ChannelType) interdit d'insérer une seconde
        // ligne au re-lien — Rebind DOIT réactiver la ligne existante avec le nouveau numéro.
        var link = ChannelIdentityLink.Create(
            Guid.NewGuid(), ChannelType.WhatsApp, "21611111111@c.us", "21611111111@c.us", Now);
        link.Deactivate();
        Assert.False(link.IsActive);

        var rebindAt = Now.AddDays(3);
        link.Rebind(" 21622222222@c.us ", "21622222222@c.us", rebindAt);

        Assert.True(link.IsActive);
        Assert.Equal("21622222222@c.us", link.ExternalUserId);
        Assert.Equal("21622222222@c.us", link.ExternalChatId);
        Assert.Equal(rebindAt, link.VerifiedAt);
        Assert.Equal(rebindAt, link.UpdatedAt);
    }

    [Fact]
    public void IdentityLink_TouchLastSeen_UpdatesTimestamp()
    {
        var link = ChannelIdentityLink.Create(
            Guid.NewGuid(), ChannelType.WhatsApp, "21611111111@c.us", "21611111111@c.us", Now);

        link.TouchLastSeen(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(5), link.LastSeenAt);
    }

    // ── ChannelLinkCode ──

    [Fact]
    public void LinkCode_RegisterAttempt_Increments()
    {
        var code = ChannelLinkCode.Create(Guid.NewGuid(), ChannelType.WhatsApp, "HASH", Now.AddMinutes(10));

        code.RegisterAttempt();
        code.RegisterAttempt();

        Assert.Equal(2, code.AttemptCount);
        Assert.Null(code.ConsumedAt);
    }

    [Fact]
    public void LinkCode_MarkConsumed_SetsConsumedAt()
    {
        var code = ChannelLinkCode.Create(Guid.NewGuid(), ChannelType.WhatsApp, "HASH", Now.AddMinutes(10));

        code.MarkConsumed(Now.AddMinutes(2));

        Assert.Equal(Now.AddMinutes(2), code.ConsumedAt);
    }

    // ── ChannelExternalRoute (index de routage master) ──

    [Fact]
    public void ExternalRoute_Create_IsActive_WithTrimmedExternalId()
    {
        var route = ChannelExternalRoute.Create(
            ChannelType.WhatsApp, " 21612345678@c.us ", Guid.NewGuid(), Guid.NewGuid());

        Assert.True(route.IsActive);
        Assert.Equal("21612345678@c.us", route.ExternalUserId);
    }

    [Fact]
    public void ExternalRoute_Rebind_ReassignsTenantAndUser_AndReactivates()
    {
        // Cas réel : le même numéro WhatsApp est re-lié à un autre compte (autre tenant).
        var route = ChannelExternalRoute.Create(
            ChannelType.WhatsApp, "21612345678@c.us", Guid.NewGuid(), Guid.NewGuid());
        route.Deactivate(Now);
        var newTenantId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();

        route.Rebind(newTenantId, newUserId, Now.AddDays(1));

        Assert.True(route.IsActive);
        Assert.Equal(newTenantId, route.TenantId);
        Assert.Equal(newUserId, route.UserId);
    }

    // ── ChannelLinkCodePointer (pointeur master) ──

    [Fact]
    public void LinkCodePointer_LifeCycle_AttemptsAndConsumption()
    {
        var pointer = ChannelLinkCodePointer.Create(
            ChannelType.WhatsApp, "HASH", Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(10));

        Assert.Null(pointer.ConsumedAt);
        pointer.RegisterAttempt();
        Assert.Equal(1, pointer.AttemptCount);

        pointer.MarkConsumed(Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), pointer.ConsumedAt);
    }
}
