using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities;

public sealed class SalesTarget : Entity
{
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = null!;
    public int Year { get; private set; }
    public int Month { get; private set; }
    public Money TargetAmount { get; private set; } = null!;

    private SalesTarget() { }

    public static Result<SalesTarget> Create(
        Guid userId,
        string userName,
        int year,
        int month,
        Money targetAmount)
    {
        if (month is < 1 or > 12)
            return Result.Failure<SalesTarget>(Error.Validation("Month", "Le mois doit être entre 1 et 12"));

        if (year < 2020)
            return Result.Failure<SalesTarget>(Error.Validation("Year", "L'année doit être >= 2020"));

        if (targetAmount.Amount <= 0)
            return Result.Failure<SalesTarget>(Error.Validation("TargetAmount", "L'objectif doit être positif"));

        return Result.Success(new SalesTarget
        {
            UserId = userId,
            UserName = userName?.Trim() ?? string.Empty,
            Year = year,
            Month = month,
            TargetAmount = targetAmount
        });
    }

    public Result Update(Money targetAmount)
    {
        if (targetAmount.Amount <= 0)
            return Result.Failure(Error.Validation("TargetAmount", "L'objectif doit être positif"));

        TargetAmount = targetAmount;
        return Result.Success();
    }
}
