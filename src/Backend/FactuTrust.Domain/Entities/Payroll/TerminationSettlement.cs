using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Solde de tout compte / rupture : indemnités de fin de contrat portées sur le bulletin du mois de sortie.
/// </summary>
public sealed class TerminationSettlement : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public DateTime TerminationDate { get; private set; }
    public TerminationReason Reason { get; private set; }
    public TerminationSettlementStatus Status { get; private set; }

    public int SeniorityMonths { get; private set; }
    public decimal GrossMonthlyReference { get; private set; }

    /// <summary>Indemnité légale de licenciement (art. 22bis CDT).</summary>
    public decimal LegalIndemnityAmount { get; private set; }
    /// <summary>Indemnité de préavis.</summary>
    public decimal NoticeIndemnityAmount { get; private set; }
    /// <summary>Indemnité de congés non consommés.</summary>
    public decimal UnusedLeaveAmount { get; private set; }
    /// <summary>Autres indemnités contractuelles.</summary>
    public decimal OtherIndemnityAmount { get; private set; }

    public string? Notes { get; private set; }

    public decimal TotalIndemnityAmount =>
        R(LegalIndemnityAmount + NoticeIndemnityAmount + UnusedLeaveAmount + OtherIndemnityAmount);

    private TerminationSettlement() { }

    public static Result<TerminationSettlement> Create(
        Guid employeeId,
        int year,
        int month,
        DateTime terminationDate,
        TerminationReason reason,
        int seniorityMonths,
        decimal grossMonthlyReference,
        decimal legalIndemnityAmount,
        decimal noticeIndemnityAmount = 0m,
        decimal unusedLeaveAmount = 0m,
        decimal otherIndemnityAmount = 0m,
        string? notes = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<TerminationSettlement>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (year is < 2000 or > 2100)
            return Result.Failure<TerminationSettlement>(Error.Validation("Year", "Année invalide."));
        if (month is < 1 or > 12)
            return Result.Failure<TerminationSettlement>(Error.Validation("Month", "Mois invalide."));
        if (grossMonthlyReference < 0)
            return Result.Failure<TerminationSettlement>(Error.Validation("GrossMonthlyReference", "Le salaire de référence ne peut pas être négatif."));

        return Result.Success(new TerminationSettlement
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            TerminationDate = terminationDate.Date,
            Reason = reason,
            Status = TerminationSettlementStatus.Calculated,
            SeniorityMonths = Math.Max(0, seniorityMonths),
            GrossMonthlyReference = R(grossMonthlyReference),
            LegalIndemnityAmount = R(legalIndemnityAmount),
            NoticeIndemnityAmount = R(noticeIndemnityAmount),
            UnusedLeaveAmount = R(unusedLeaveAmount),
            OtherIndemnityAmount = R(otherIndemnityAmount),
            Notes = Normalize(notes)
        });
    }

    public Result Refresh(
        int seniorityMonths,
        decimal grossMonthlyReference,
        decimal legalIndemnityAmount,
        decimal noticeIndemnityAmount,
        decimal unusedLeaveAmount,
        decimal otherIndemnityAmount,
        string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce solde ne peut plus être recalculé."));

        SeniorityMonths = Math.Max(0, seniorityMonths);
        GrossMonthlyReference = R(grossMonthlyReference);
        LegalIndemnityAmount = R(legalIndemnityAmount);
        NoticeIndemnityAmount = R(noticeIndemnityAmount);
        UnusedLeaveAmount = R(unusedLeaveAmount);
        OtherIndemnityAmount = R(otherIndemnityAmount);
        if (notes is not null)
            Notes = Normalize(notes);
        Status = TerminationSettlementStatus.Calculated;
        IncrementVersion();
        return Result.Success();
    }

    public Result SetManualAmounts(
        decimal legalIndemnityAmount,
        decimal noticeIndemnityAmount,
        decimal unusedLeaveAmount,
        decimal otherIndemnityAmount,
        string? notes = null)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce solde ne peut plus être modifié."));

        LegalIndemnityAmount = R(legalIndemnityAmount);
        NoticeIndemnityAmount = R(noticeIndemnityAmount);
        UnusedLeaveAmount = R(unusedLeaveAmount);
        OtherIndemnityAmount = R(otherIndemnityAmount);
        Notes = Normalize(notes) ?? Notes;
        IncrementVersion();
        return Result.Success();
    }

    public Result Approve()
    {
        if (Status is not TerminationSettlementStatus.Calculated and not TerminationSettlementStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Seul un solde calculé peut être approuvé."));
        Status = TerminationSettlementStatus.Approved;
        IncrementVersion();
        return Result.Success();
    }

    public Result MarkPaid()
    {
        if (Status != TerminationSettlementStatus.Approved)
            return Result.Failure(Error.Validation("Status", "Seul un solde approuvé peut être marqué payé."));
        Status = TerminationSettlementStatus.Paid;
        IncrementVersion();
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status is TerminationSettlementStatus.Paid)
            return Result.Failure(Error.Validation("Status", "Un solde payé ne peut pas être annulé."));
        Status = TerminationSettlementStatus.Cancelled;
        IncrementVersion();
        return Result.Success();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
