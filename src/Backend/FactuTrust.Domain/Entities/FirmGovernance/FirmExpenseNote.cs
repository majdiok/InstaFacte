using FactuTrust.Domain.Common;



namespace FactuTrust.Domain.Entities.FirmGovernance;



public enum FirmExpenseNoteStatus

{

    Draft = 0,

    Submitted = 1,

    Approved = 2,

    Rejected = 3,

    Reimbursed = 4

}



public sealed class FirmExpenseNote : AggregateRoot

{

    public Guid FirmTenantId { get; private set; }

    public Guid FirmClientAssignmentId { get; private set; }

    public string CompanyName { get; private set; } = null!;

    public Guid? LegalRepresentativeId { get; private set; }

    public string? RepresentativeName { get; private set; }

    public int PeriodYear { get; private set; }

    public int PeriodMonth { get; private set; }

    public FirmExpenseNoteStatus Status { get; private set; }

    public decimal TotalToReimburse { get; private set; }

    public decimal MixedCharges { get; private set; }

    public decimal OperatingExpenses { get; private set; }

    public decimal MileageAllowance { get; private set; }

    public decimal SalesAmount { get; private set; }

    public string? Notes { get; private set; }



    private FirmExpenseNote() { }



    public static Result<FirmExpenseNote> Create(

        Guid firmTenantId,

        Guid assignmentId,

        string companyName,

        int periodYear,

        int periodMonth)

    {

        if (firmTenantId == Guid.Empty || assignmentId == Guid.Empty)

            return Result.Failure<FirmExpenseNote>(Error.Validation("Assignment", "Dossier client requis"));



        return Result.Success(new FirmExpenseNote

        {

            FirmTenantId = firmTenantId,

            FirmClientAssignmentId = assignmentId,

            CompanyName = companyName.Trim(),

            PeriodYear = periodYear,

            PeriodMonth = periodMonth,

            Status = FirmExpenseNoteStatus.Draft

        });

    }



    public Result UpdateAmounts(

        decimal totalToReimburse,

        decimal mixedCharges,

        decimal operatingExpenses,

        decimal mileageAllowance,

        decimal salesAmount,

        string? notes)

    {

        if (Status is FirmExpenseNoteStatus.Approved or FirmExpenseNoteStatus.Reimbursed)

            return Result.Failure(Error.Validation("Status", "Note de frais verrouillée."));



        TotalToReimburse = Math.Max(0, totalToReimburse);

        MixedCharges = Math.Max(0, mixedCharges);

        OperatingExpenses = Math.Max(0, operatingExpenses);

        MileageAllowance = Math.Max(0, mileageAllowance);

        SalesAmount = Math.Max(0, salesAmount);

        Notes = notes?.Trim();

        return Result.Success();

    }



    public Result Submit()

    {

        if (Status is not (FirmExpenseNoteStatus.Draft or FirmExpenseNoteStatus.Rejected))

            return Result.Failure(Error.Validation("Status", "Seules les notes brouillon ou rejetées peuvent être soumises."));



        Status = FirmExpenseNoteStatus.Submitted;

        return Result.Success();

    }



    public Result Approve()

    {

        if (Status != FirmExpenseNoteStatus.Submitted)

            return Result.Failure(Error.Validation("Status", "Seules les notes soumises peuvent être approuvées."));



        Status = FirmExpenseNoteStatus.Approved;

        return Result.Success();

    }



    public Result Reject()

    {

        if (Status != FirmExpenseNoteStatus.Submitted)

            return Result.Failure(Error.Validation("Status", "Seules les notes soumises peuvent être rejetées."));



        Status = FirmExpenseNoteStatus.Rejected;

        return Result.Success();

    }



    public Result MarkReimbursed()

    {

        if (Status != FirmExpenseNoteStatus.Approved)

            return Result.Failure(Error.Validation("Status", "Seules les notes approuvées peuvent être marquées remboursées."));



        Status = FirmExpenseNoteStatus.Reimbursed;

        return Result.Success();

    }

}



