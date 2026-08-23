using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Server-side POS cart snapshot, keyed by cashier + register (tenant DB isolation).
/// </summary>
public sealed class PosCartDraft : AggregateRoot
{
    public Guid UserId { get; private set; }
    public Guid CashRegisterId { get; private set; }
    public string StateJson { get; private set; } = null!;

    private PosCartDraft() { }

    public static Result<PosCartDraft> Create(Guid userId, Guid cashRegisterId, string stateJson)
    {
        if (userId == Guid.Empty)
            return Result.Failure<PosCartDraft>(
                Error.Validation("UserId", "L'utilisateur est obligatoire"));

        if (cashRegisterId == Guid.Empty)
            return Result.Failure<PosCartDraft>(
                Error.Validation("CashRegisterId", "La caisse est obligatoire"));

        if (string.IsNullOrWhiteSpace(stateJson))
            return Result.Failure<PosCartDraft>(
                Error.Validation("StateJson", "L'état du panier POS est obligatoire"));

        var draft = new PosCartDraft
        {
            UserId = userId,
            CashRegisterId = cashRegisterId,
            StateJson = stateJson
        };

        return Result.Success(draft);
    }

    public Result ReplaceState(string stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
            return Result.Failure(Error.Validation("StateJson", "L'état du panier POS est obligatoire"));

        StateJson = stateJson;
        IncrementVersion();
        return Result.Success();
    }
}
