using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Parked POS ticket (held order) persisted in the tenant database.
/// </summary>
public sealed class PosHeldTicket : AggregateRoot
{
    public Guid CashRegisterId { get; private set; }
    public Guid? CashRegisterSessionId { get; private set; }
    public Guid HeldByUserId { get; private set; }
    public string Label { get; private set; } = null!;
    public decimal TotalTtc { get; private set; }
    public int LineCount { get; private set; }
    public string StateJson { get; private set; } = null!;
    public DateTime HeldAt { get; private set; }

    private PosHeldTicket() { }

    public static Result<PosHeldTicket> Create(
        Guid cashRegisterId,
        Guid heldByUserId,
        string label,
        decimal totalTtc,
        int lineCount,
        string stateJson,
        Guid? cashRegisterSessionId = null,
        Guid? id = null)
    {
        if (cashRegisterId == Guid.Empty)
            return Result.Failure<PosHeldTicket>(
                Error.Validation("CashRegisterId", "La caisse est obligatoire"));

        if (heldByUserId == Guid.Empty)
            return Result.Failure<PosHeldTicket>(
                Error.Validation("HeldByUserId", "L'utilisateur est obligatoire"));

        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<PosHeldTicket>(
                Error.Validation("Label", "Le libellé du ticket en attente est obligatoire"));

        var labelTrimmed = label.Trim();
        if (labelTrimmed.Length > 200)
            return Result.Failure<PosHeldTicket>(
                Error.Validation("Label", "Le libellé ne peut pas dépasser 200 caractères"));

        if (lineCount < 1)
            return Result.Failure<PosHeldTicket>(
                Error.Validation("LineCount", "Le ticket en attente doit contenir au moins une ligne"));

        if (totalTtc < 0)
            return Result.Failure<PosHeldTicket>(
                Error.Validation("TotalTtc", "Le total TTC ne peut pas être négatif"));

        if (string.IsNullOrWhiteSpace(stateJson))
            return Result.Failure<PosHeldTicket>(
                Error.Validation("StateJson", "L'état du ticket en attente est obligatoire"));

        var ticket = new PosHeldTicket
        {
            CashRegisterId = cashRegisterId,
            CashRegisterSessionId = cashRegisterSessionId,
            HeldByUserId = heldByUserId,
            Label = labelTrimmed,
            TotalTtc = Math.Round(totalTtc, 3),
            LineCount = lineCount,
            StateJson = stateJson,
            HeldAt = DateTime.UtcNow
        };

        if (id is { } presetId && presetId != Guid.Empty)
            ticket.Id = presetId;

        return Result.Success(ticket);
    }
}
