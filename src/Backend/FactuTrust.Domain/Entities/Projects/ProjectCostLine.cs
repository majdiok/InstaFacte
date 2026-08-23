using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>
/// Operational project cost line. Does not post to the general ledger.
/// </summary>
public sealed class ProjectCostLine : Entity
{
    public Guid ProjectId { get; private set; }
    public ProjectCostSource Source { get; private set; }
    public Guid? SourceId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal AmountHt { get; private set; }
    public DateTime OccurredOn { get; private set; }
    public Guid? TimeEntryId { get; private set; }

    private ProjectCostLine() { }

    public static Result<ProjectCostLine> Create(
        Guid projectId,
        ProjectCostSource source,
        string description,
        decimal amountHt,
        DateTime occurredOn,
        Guid? sourceId = null,
        Guid? timeEntryId = null)
    {
        description = description?.Trim() ?? string.Empty;
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectCostLine>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (string.IsNullOrEmpty(description))
            return Result.Failure<ProjectCostLine>(Error.Validation("Description", "La description est obligatoire"));
        if (description.Length > 500)
            description = description[..500];
        if (amountHt < 0)
            return Result.Failure<ProjectCostLine>(Error.Validation("AmountHt", "Le montant ne peut pas être négatif"));

        return Result.Success(new ProjectCostLine
        {
            ProjectId = projectId,
            Source = source,
            SourceId = sourceId,
            Description = description,
            AmountHt = decimal.Round(amountHt, 3),
            OccurredOn = occurredOn.Date,
            TimeEntryId = timeEntryId
        });
    }
}
