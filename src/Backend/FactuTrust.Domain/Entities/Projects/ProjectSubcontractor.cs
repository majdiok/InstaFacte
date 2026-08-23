using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectSubcontractor : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string? ContractReference { get; private set; }
    public decimal AmountHt { get; private set; }
    public decimal RetainagePercent { get; private set; }

    private ProjectSubcontractor() { }

    public static Result<ProjectSubcontractor> Create(
        Guid projectId,
        Guid supplierId,
        string? contractReference,
        decimal amountHt,
        decimal retainagePercent)
    {
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectSubcontractor>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (supplierId == Guid.Empty)
            return Result.Failure<ProjectSubcontractor>(Error.Validation("SupplierId", "Le fournisseur est obligatoire"));
        if (amountHt < 0)
            return Result.Failure<ProjectSubcontractor>(Error.Validation("AmountHt", "Le montant ne peut pas être négatif"));
        if (retainagePercent is < 0 or > 100)
            return Result.Failure<ProjectSubcontractor>(Error.Validation("RetainagePercent", "La retenue doit être entre 0 et 100"));

        return Result.Success(new ProjectSubcontractor
        {
            ProjectId = projectId,
            SupplierId = supplierId,
            ContractReference = contractReference?.Trim(),
            AmountHt = decimal.Round(amountHt, 3),
            RetainagePercent = decimal.Round(retainagePercent, 2)
        });
    }

    public Result Update(string? contractReference, decimal amountHt, decimal retainagePercent)
    {
        if (amountHt < 0)
            return Result.Failure(Error.Validation("AmountHt", "Le montant ne peut pas être négatif"));
        if (retainagePercent is < 0 or > 100)
            return Result.Failure(Error.Validation("RetainagePercent", "La retenue doit être entre 0 et 100"));

        ContractReference = contractReference?.Trim();
        AmountHt = decimal.Round(amountHt, 3);
        RetainagePercent = decimal.Round(retainagePercent, 2);
        return Result.Success();
    }
}
