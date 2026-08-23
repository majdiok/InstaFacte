using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Cashier shift (vacation) on a <see cref="CashRegister"/>. Opening float and closing count
/// are declarations, not <see cref="CashOperation"/> rows.
/// </summary>
public sealed class CashRegisterSession : AggregateRoot
{
    public Guid CashRegisterId { get; private set; }
    public CashRegister CashRegister { get; private set; } = null!;

    public CashRegisterSessionStatus Status { get; private set; }

    public DateTime OpenedAt { get; private set; }
    public Guid OpenedByUserId { get; private set; }
    public Money OpeningFloat { get; private set; } = null!;

    public DateTime? ClosedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public Money? ClosingCountedCash { get; private set; }
    public Money? ClosingExpectedCash { get; private set; }
    public Money? CashVariance { get; private set; }

    public Guid? ZReportId { get; private set; }

    private CashRegisterSession() { }

    public static Result<CashRegisterSession> Open(
        Guid cashRegisterId,
        Guid openedByUserId,
        Money openingFloat)
    {
        if (cashRegisterId == Guid.Empty)
            return Result.Failure<CashRegisterSession>(
                Error.Validation("CashRegisterId", "La caisse est obligatoire"));

        if (openedByUserId == Guid.Empty)
            return Result.Failure<CashRegisterSession>(
                Error.Validation("OpenedByUserId", "L'utilisateur d'ouverture est obligatoire"));

        if (openingFloat is null)
            return Result.Failure<CashRegisterSession>(
                Error.Validation("OpeningFloat", "Le fond de caisse est obligatoire"));

        var session = new CashRegisterSession
        {
            CashRegisterId = cashRegisterId,
            Status = CashRegisterSessionStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = openedByUserId,
            OpeningFloat = Money.Create(openingFloat.Amount, openingFloat.Currency)
        };

        return Result.Success(session);
    }

    public Result Close(Guid closedByUserId, Money countedCash, Money expectedCash, Guid zReportId)
    {
        if (Status == CashRegisterSessionStatus.Closed)
            return Result.Failure(Error.Validation("Status", "Cette session de caisse est déjà clôturée"));

        if (closedByUserId == Guid.Empty)
            return Result.Failure(Error.Validation("ClosedByUserId", "L'utilisateur de clôture est obligatoire"));

        if (zReportId == Guid.Empty)
            return Result.Failure(Error.Validation("ZReportId", "Le rapport Z est obligatoire"));

        if (countedCash is null)
            return Result.Failure(Error.Validation("CountedCash", "Le comptage espèces est obligatoire"));

        if (expectedCash is null)
            return Result.Failure(Error.Validation("ExpectedCash", "Le théorique espèces est obligatoire"));

        if (countedCash.Amount < 0)
            return Result.Failure(Error.Validation("CountedCash", "Le comptage espèces ne peut pas être négatif"));

        var currency = OpeningFloat.Currency;
        ClosingCountedCash = Money.Create(countedCash.Amount, countedCash.Currency);
        ClosingExpectedCash = Money.FromSignedAmount(expectedCash.Amount, expectedCash.Currency);
        CashVariance = Money.FromSignedAmount(
            countedCash.Amount - expectedCash.Amount,
            currency);
        ClosedAt = DateTime.UtcNow;
        ClosedByUserId = closedByUserId;
        ZReportId = zReportId;
        Status = CashRegisterSessionStatus.Closed;
        IncrementVersion();
        return Result.Success();
    }
}
