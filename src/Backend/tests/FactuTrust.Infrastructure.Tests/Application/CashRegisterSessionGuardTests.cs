using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.CashRegister;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CashRegisterSessionGuardTests
{
    [Fact]
    public async Task ResolveOpenId_WithoutSession_ReturnsNull()
    {
        var sessions = new Mock<ICashRegisterSessionRepository>(MockBehavior.Strict);

        var result = await CashRegisterSessionGuard.ResolveOpenIdAsync(
            sessions.Object, null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        sessions.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveOpenId_OpenSession_ReturnsId()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(10m)).Value;
        var sessions = new Mock<ICashRegisterSessionRepository>();
        sessions
            .Setup(s => s.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await CashRegisterSessionGuard.ResolveOpenIdAsync(
            sessions.Object, session.Id, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(session.Id, result.Value);
    }

    [Fact]
    public async Task ResolveOpenId_ClosedSession_ReturnsPosSessionClosed()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Zero()).Value;
        Assert.True(session.Close(Guid.NewGuid(), Money.Zero(), Money.Zero(), Guid.NewGuid()).IsSuccess);

        var sessions = new Mock<ICashRegisterSessionRepository>();
        sessions
            .Setup(s => s.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await CashRegisterSessionGuard.ResolveOpenIdAsync(
            sessions.Object, session.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("POS_SESSION_CLOSED", result.Error.Code);
    }

    [Fact]
    public async Task ResolveOpenId_InheritsInvoiceSessionWhenRequestOmitsIt()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(5m)).Value;
        var sessions = new Mock<ICashRegisterSessionRepository>();
        sessions
            .Setup(s => s.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await CashRegisterSessionGuard.ResolveOpenIdAsync(
            sessions.Object, null, session.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(session.Id, result.Value);
    }
}
