using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectActivity : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Message { get; private set; } = null!;

    private ProjectActivity() { }

    public static ProjectActivity Create(Guid projectId, string type, string message, Guid? actorUserId = null, Guid? taskId = null)
    {
        return new ProjectActivity
        {
            ProjectId = projectId,
            TaskId = taskId,
            ActorUserId = actorUserId,
            Type = (type ?? "info").Trim(),
            Message = (message ?? string.Empty).Trim()
        };
    }
}
