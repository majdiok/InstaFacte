using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectMilestone : Entity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Percent { get; private set; }
    public decimal AmountHt { get; private set; }
    public DateTime? DueDate { get; private set; }
    public Guid? InvoicedInvoiceId { get; private set; }
    public Guid? SalesOrderLineId { get; private set; }
    public bool IsReached { get; private set; }
    public DateTime? ReachedAt { get; private set; }

    private ProjectMilestone() { }

    public static Result<ProjectMilestone> Create(
        Guid projectId,
        string name,
        decimal percent,
        decimal amountHt,
        DateTime? dueDate,
        Guid? salesOrderLineId = null)
    {
        name = name?.Trim() ?? string.Empty;
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectMilestone>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (string.IsNullOrEmpty(name))
            return Result.Failure<ProjectMilestone>(Error.Validation("Name", "Le nom du jalon est obligatoire"));
        if (percent is < 0 or > 100)
            return Result.Failure<ProjectMilestone>(Error.Validation("Percent", "Le pourcentage doit être entre 0 et 100"));
        if (amountHt < 0)
            return Result.Failure<ProjectMilestone>(Error.Validation("AmountHt", "Le montant ne peut pas être négatif"));

        return Result.Success(new ProjectMilestone
        {
            ProjectId = projectId,
            Name = name.Length > 200 ? name[..200] : name,
            Percent = decimal.Round(percent, 2),
            AmountHt = decimal.Round(amountHt, 3),
            DueDate = dueDate?.Date,
            SalesOrderLineId = salesOrderLineId == Guid.Empty ? null : salesOrderLineId
        });
    }

    public Result MarkReached(bool manual = false)
    {
        if (IsReached)
            return Result.Success();
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("Status", "Un jalon déjà facturé ne peut pas être modifié"));
        IsReached = true;
        ReachedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result MarkUnreached()
    {
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("Status", "Un jalon déjà facturé ne peut pas être modifié"));
        IsReached = false;
        ReachedAt = null;
        return Result.Success();
    }

    public Result Update(string name, decimal percent, decimal amountHt, DateTime? dueDate)
    {
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("Status", "Un jalon déjà facturé ne peut pas être modifié"));
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return Result.Failure(Error.Validation("Name", "Le nom du jalon est obligatoire"));
        if (percent is < 0 or > 100)
            return Result.Failure(Error.Validation("Percent", "Le pourcentage doit être entre 0 et 100"));
        if (amountHt < 0)
            return Result.Failure(Error.Validation("AmountHt", "Le montant ne peut pas être négatif"));

        Name = name.Length > 200 ? name[..200] : name;
        Percent = decimal.Round(percent, 2);
        AmountHt = decimal.Round(amountHt, 3);
        DueDate = dueDate?.Date;
        return Result.Success();
    }

    public Result MarkInvoiced(Guid invoiceId)
    {
        if (InvoicedInvoiceId.HasValue)
            return Result.Failure(Error.Validation("InvoicedInvoiceId", "Ce jalon a déjà été facturé"));
        if (invoiceId == Guid.Empty)
            return Result.Failure(Error.Validation("InvoiceId", "La facture est obligatoire"));
        InvoicedInvoiceId = invoiceId;
        return Result.Success();
    }
}
