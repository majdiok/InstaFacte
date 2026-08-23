using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.CashRegister;

internal static class CashRegisterSessionGuard
{
    public static readonly Error Closed = new(
        "POS_SESSION_CLOSED",
        "La session de caisse est clôturée");

    public static async Task<Result<Guid?>> ResolveOpenIdAsync(
        ICashRegisterSessionRepository sessions,
        Guid? requestedSessionId,
        Guid? inheritedSessionId,
        CancellationToken cancellationToken)
    {
        Guid? sessionId = requestedSessionId is { } requested && requested != Guid.Empty
            ? requested
            : inheritedSessionId is { } inherited && inherited != Guid.Empty
                ? inherited
                : (Guid?)null;

        if (sessionId is null)
            return Result.Success<Guid?>(null);

        var session = await sessions.GetByIdAsync(sessionId.Value, cancellationToken);
        if (session is null)
            return Result.Failure<Guid?>(Error.NotFound("CashRegisterSession", sessionId.Value));

        if (session.Status != CashRegisterSessionStatus.Open)
            return Result.Failure<Guid?>(Closed);

        return Result.Success<Guid?>(session.Id);
    }
}
