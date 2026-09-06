using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>
/// Company project time entry. Isolated from cabinet <c>FirmTimeSheetEntry</c> (Master DB).
/// </summary>
public sealed class ProjectTimeEntry : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime WorkDate { get; private set; }
    public decimal Hours { get; private set; }
    public bool IsBillable { get; private set; }
    public string? Notes { get; private set; }
    public ProjectTimeEntryStatus Status { get; private set; }
    public Guid? InvoicedInvoiceId { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? ValidatedAt { get; private set; }

    public bool IsInvoiced => InvoicedInvoiceId.HasValue;

    private ProjectTimeEntry() { }

    public static Result<ProjectTimeEntry> Create(
        Guid projectId,
        Guid userId,
        DateTime workDate,
        decimal hours,
        bool isBillable,
        string? notes,
        Guid? taskId)
    {
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectTimeEntry>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (userId == Guid.Empty)
            return Result.Failure<ProjectTimeEntry>(Error.Validation("UserId", "L'utilisateur est obligatoire"));
        if (hours <= 0 || hours > 24)
            return Result.Failure<ProjectTimeEntry>(Error.Validation("Hours", "Les heures doivent être comprises entre 0 et 24"));

        return Result.Success(new ProjectTimeEntry
        {
            ProjectId = projectId,
            TaskId = taskId,
            UserId = userId,
            WorkDate = workDate.Date,
            Hours = decimal.Round(hours, 2),
            IsBillable = isBillable,
            Notes = notes?.Trim(),
            Status = ProjectTimeEntryStatus.Draft
        });
    }

    public Result Update(DateTime workDate, decimal hours, bool isBillable, string? notes, Guid? taskId)
    {
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("Status", "Un temps facturé est immuable"));
        if (Status != ProjectTimeEntryStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Seuls les temps en brouillon peuvent être modifiés"));
        if (hours <= 0 || hours > 24)
            return Result.Failure(Error.Validation("Hours", "Les heures doivent être comprises entre 0 et 24"));

        WorkDate = workDate.Date;
        Hours = decimal.Round(hours, 2);
        IsBillable = isBillable;
        Notes = notes?.Trim();
        TaskId = taskId;
        return Result.Success();
    }

    public Result Submit()
    {
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("Status", "Un temps facturé est immuable"));
        if (Status != ProjectTimeEntryStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Seuls les temps en brouillon peuvent être soumis"));
        if (Hours <= 0)
            return Result.Failure(Error.Validation("Hours", "Les heures doivent être strictement supérieures à 0"));
        Status = ProjectTimeEntryStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Validate()
    {
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("Status", "Un temps facturé est immuable"));
        if (Status != ProjectTimeEntryStatus.Submitted)
            return Result.Failure(Error.Validation("Status", "Seuls les temps soumis peuvent être validés"));
        if (Hours <= 0)
            return Result.Failure(Error.Validation("Hours", "Les heures doivent être strictement supérieures à 0"));
        Status = ProjectTimeEntryStatus.Validated;
        ValidatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result ReopenToDraft()
    {
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("Status", "Un temps déjà facturé ne peut pas être rouvert"));
        if (Status == ProjectTimeEntryStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Ce temps est déjà en brouillon"));
        if (Status is not (ProjectTimeEntryStatus.Submitted or ProjectTimeEntryStatus.Validated))
            return Result.Failure(Error.Validation("Status", "Seuls les temps soumis ou validés peuvent être rouverts"));
        Status = ProjectTimeEntryStatus.Draft;
        SubmittedAt = null;
        ValidatedAt = null;
        return Result.Success();
    }

    public Result MarkInvoiced(Guid invoiceId)
    {
        if (Status != ProjectTimeEntryStatus.Validated)
            return Result.Failure(Error.Validation("Status", "Seuls les temps validés peuvent être facturés"));
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("InvoicedInvoiceId", "Ces heures ont déjà été facturées"));
        if (!IsBillable)
            return Result.Failure(Error.Validation("IsBillable", "Ces heures ne sont pas facturables"));
        if (invoiceId == Guid.Empty)
            return Result.Failure(Error.Validation("InvoiceId", "La facture est obligatoire"));

        InvoicedInvoiceId = invoiceId;
        return Result.Success();
    }

    public bool CanBeDeleted() =>
        Status == ProjectTimeEntryStatus.Draft && !InvoicedInvoiceId.HasValue;

    public bool IsOpen => Status is ProjectTimeEntryStatus.Draft or ProjectTimeEntryStatus.Submitted;
}
