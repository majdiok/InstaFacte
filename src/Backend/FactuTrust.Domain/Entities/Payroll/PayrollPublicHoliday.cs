using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>Jour férié tunisien (fixe ou islamique) pour le décompte des jours ouvrables paie.</summary>
public sealed class PayrollPublicHoliday : Entity
{
    public int Year { get; private set; }
    public DateTime Date { get; private set; }
    public string Label { get; private set; } = null!;
    public PublicHolidayKind Kind { get; private set; }
    public bool IsPaid { get; private set; }
    public bool IsEstimated { get; private set; }
    public string? DecreeReference { get; private set; }

    private PayrollPublicHoliday() { }

    public static Result<PayrollPublicHoliday> Create(
        int year,
        DateTime date,
        string label,
        PublicHolidayKind kind,
        bool isPaid = true,
        bool isEstimated = false,
        string? decreeReference = null)
    {
        if (year is < 2000 or > 2100)
            return Result.Failure<PayrollPublicHoliday>(Error.Validation("Year", "L'exercice doit être compris entre 2000 et 2100."));
        if (date.Year != year)
            return Result.Failure<PayrollPublicHoliday>(Error.Validation("Date", "La date doit appartenir à l'exercice indiqué."));
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<PayrollPublicHoliday>(Error.Validation("Label", "Le libellé est obligatoire."));

        return Result.Success(new PayrollPublicHoliday
        {
            Year = year,
            Date = date.Date,
            Label = label.Trim(),
            Kind = kind,
            IsPaid = isPaid,
            IsEstimated = isEstimated,
            DecreeReference = string.IsNullOrWhiteSpace(decreeReference) ? null : decreeReference.Trim()
        });
    }

    public Result Update(
        DateTime date,
        string label,
        PublicHolidayKind kind,
        bool isPaid,
        bool isEstimated,
        string? decreeReference)
    {
        if (date.Year != Year)
            return Result.Failure(Error.Validation("Date", "La date doit appartenir à l'exercice du jour férié."));
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));

        Date = date.Date;
        Label = label.Trim();
        Kind = kind;
        IsPaid = isPaid;
        IsEstimated = isEstimated;
        DecreeReference = string.IsNullOrWhiteSpace(decreeReference) ? null : decreeReference.Trim();
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }
}
