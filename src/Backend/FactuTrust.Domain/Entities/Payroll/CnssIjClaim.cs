using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Créance IJ CNSS à récupérer auprès de la Caisse (subrogation ou indemnisation maternité).
/// </summary>
public sealed class CnssIjClaim : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal Amount { get; private set; }
    public CnssIjClaimStatus Status { get; private set; }
    public DateTime? PaidAt { get; private set; }

    private CnssIjClaim() { }

    public static Result<CnssIjClaim> Create(
        Guid employeeId,
        Guid leaveRequestId,
        int year,
        int month,
        decimal amount)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<CnssIjClaim>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (leaveRequestId == Guid.Empty)
            return Result.Failure<CnssIjClaim>(Error.Validation("LeaveRequestId", "Le congé est obligatoire."));
        if (year < 2000 || year > 2100)
            return Result.Failure<CnssIjClaim>(Error.Validation("Year", "Exercice invalide."));
        if (month is < 1 or > 12)
            return Result.Failure<CnssIjClaim>(Error.Validation("Month", "Mois invalide."));

        var rounded = R(amount);
        if (rounded <= 0)
            return Result.Failure<CnssIjClaim>(Error.Validation("Amount", "Le montant IJ doit être strictement positif."));

        return Result.Success(new CnssIjClaim
        {
            EmployeeId = employeeId,
            LeaveRequestId = leaveRequestId,
            Year = year,
            Month = month,
            Amount = rounded,
            Status = CnssIjClaimStatus.Pending
        });
    }

    public Result MarkPaid(DateTime paidAt)
    {
        if (Status == CnssIjClaimStatus.Paid)
            return Result.Success();

        if (Status == CnssIjClaimStatus.Rejected)
        {
            return Result.Failure(Error.Validation(
                "Status",
                "Une créance IJ rejetée ne peut pas être marquée comme réglée."));
        }

        if (paidAt > DateTime.UtcNow.AddDays(1))
        {
            return Result.Failure(Error.Validation(
                "PaidAt",
                "La date de règlement ne peut pas être dans le futur."));
        }

        Status = CnssIjClaimStatus.Paid;
        PaidAt = paidAt.Date;
        IncrementVersion();
        return Result.Success();
    }

    public Result Reject()
    {
        if (Status == CnssIjClaimStatus.Paid)
        {
            return Result.Failure(Error.Validation(
                "Status",
                "Une créance IJ déjà réglée ne peut pas être rejetée."));
        }

        Status = CnssIjClaimStatus.Rejected;
        IncrementVersion();
        return Result.Success();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
