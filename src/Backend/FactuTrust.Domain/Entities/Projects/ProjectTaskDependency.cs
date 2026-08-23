using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectTaskDependency : Entity
{
    public Guid PredecessorTaskId { get; private set; }
    public Guid SuccessorTaskId { get; private set; }

    private ProjectTaskDependency() { }

    public static Result<ProjectTaskDependency> Create(Guid predecessorTaskId, Guid successorTaskId)
    {
        if (predecessorTaskId == Guid.Empty || successorTaskId == Guid.Empty)
            return Result.Failure<ProjectTaskDependency>(Error.Validation("Task", "Les tâches prédécésseur et successeur sont obligatoires"));
        if (predecessorTaskId == successorTaskId)
            return Result.Failure<ProjectTaskDependency>(Error.Validation("Task", "Une tâche ne peut pas dépendre d'elle-même"));

        return Result.Success(new ProjectTaskDependency
        {
            PredecessorTaskId = predecessorTaskId,
            SuccessorTaskId = successorTaskId
        });
    }
}
