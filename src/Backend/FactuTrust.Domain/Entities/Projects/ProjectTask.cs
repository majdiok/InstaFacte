using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectTask : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid PhaseId { get; private set; }
    public Guid? ParentTaskId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public ProjectTaskStatus Status { get; private set; }
    public ProjectTaskPriority Priority { get; private set; }
    public DateTime? DueDate { get; private set; }
    public int ProgressPercent { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public decimal EstimatedHours { get; private set; }

    private ProjectTask() { }

    public static Result<ProjectTask> Create(
        Guid projectId,
        Guid phaseId,
        string title,
        string? description,
        ProjectTaskPriority priority,
        DateTime? dueDate,
        Guid? assigneeUserId,
        Guid? employeeId,
        decimal estimatedHours,
        Guid? parentTaskId = null)
    {
        title = title?.Trim() ?? string.Empty;
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectTask>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (phaseId == Guid.Empty)
            return Result.Failure<ProjectTask>(Error.Validation("PhaseId", "La colonne Kanban est obligatoire"));
        if (string.IsNullOrEmpty(title))
            return Result.Failure<ProjectTask>(Error.Validation("Title", "Le titre est obligatoire"));
        if (title.Length > 300)
            title = title[..300];
        if (estimatedHours < 0)
            return Result.Failure<ProjectTask>(Error.Validation("EstimatedHours", "L'estimation ne peut pas être négative"));
        if (parentTaskId == Guid.Empty)
            parentTaskId = null;

        return Result.Success(new ProjectTask
        {
            ProjectId = projectId,
            PhaseId = phaseId,
            ParentTaskId = parentTaskId,
            Title = title,
            Description = description?.Trim(),
            Status = ProjectTaskStatus.Todo,
            Priority = priority,
            DueDate = dueDate?.Date,
            ProgressPercent = 0,
            AssigneeUserId = assigneeUserId,
            EmployeeId = employeeId,
            EstimatedHours = decimal.Round(estimatedHours, 2)
        });
    }

    public Result Update(
        string title,
        string? description,
        ProjectTaskPriority priority,
        DateTime? dueDate,
        Guid? assigneeUserId,
        Guid? employeeId,
        decimal estimatedHours,
        int progressPercent)
    {
        title = title?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title))
            return Result.Failure(Error.Validation("Title", "Le titre est obligatoire"));
        if (estimatedHours < 0)
            return Result.Failure(Error.Validation("EstimatedHours", "L'estimation ne peut pas être négative"));
        if (progressPercent is < 0 or > 100)
            return Result.Failure(Error.Validation("ProgressPercent", "L'avancement doit être entre 0 et 100"));

        Title = title.Length > 300 ? title[..300] : title;
        Description = description?.Trim();
        Priority = priority;
        DueDate = dueDate?.Date;
        AssigneeUserId = assigneeUserId;
        EmployeeId = employeeId;
        EstimatedHours = decimal.Round(estimatedHours, 2);
        ProgressPercent = progressPercent;
        return Result.Success();
    }

    public Result MoveToPhase(Guid phaseId, ProjectTaskStatus? status)
    {
        if (phaseId == Guid.Empty)
            return Result.Failure(Error.Validation("PhaseId", "La colonne Kanban est obligatoire"));
        PhaseId = phaseId;
        if (status.HasValue)
        {
            Status = status.Value;
            if (status.Value == ProjectTaskStatus.Done)
                ProgressPercent = 100;
        }
        return Result.Success();
    }

    public Result SetStatus(ProjectTaskStatus status)
    {
        Status = status;
        if (status == ProjectTaskStatus.Done)
            ProgressPercent = 100;
        return Result.Success();
    }
}
