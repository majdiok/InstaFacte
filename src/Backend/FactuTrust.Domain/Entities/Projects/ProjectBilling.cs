using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>
/// Billing document emitted from a project. The commercial <c>Invoice</c> is linked via
/// <c>SourceProjectBillingId</c> — this row is the audit trail, not a second invoice.
/// </summary>
public sealed class ProjectBilling : Entity
{
    public Guid ProjectId { get; private set; }
    public ProjectBillingKind Kind { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string? Notes { get; private set; }
    public decimal AmountHt { get; private set; }

    private ProjectBilling() { }

    public static Result<ProjectBilling> Create(
        Guid projectId,
        ProjectBillingKind kind,
        Guid invoiceId,
        decimal amountHt,
        string? notes)
    {
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectBilling>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (invoiceId == Guid.Empty)
            return Result.Failure<ProjectBilling>(Error.Validation("InvoiceId", "La facture est obligatoire"));
        if (amountHt < 0)
            return Result.Failure<ProjectBilling>(Error.Validation("AmountHt", "Le montant ne peut pas être négatif"));

        return Result.Success(new ProjectBilling
        {
            ProjectId = projectId,
            Kind = kind,
            InvoiceId = invoiceId,
            AmountHt = decimal.Round(amountHt, 3),
            Notes = notes?.Trim()
        });
    }
}
