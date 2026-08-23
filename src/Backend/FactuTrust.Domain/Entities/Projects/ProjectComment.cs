using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectComment : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = null!;

    private ProjectComment() { }

    public static Result<ProjectComment> Create(Guid projectId, Guid authorUserId, string body, Guid? taskId = null)
    {
        body = body?.Trim() ?? string.Empty;
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectComment>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (string.IsNullOrEmpty(body))
            return Result.Failure<ProjectComment>(Error.Validation("Body", "Le commentaire est obligatoire"));
        if (body.Length > 4000)
            body = body[..4000];

        return Result.Success(new ProjectComment
        {
            ProjectId = projectId,
            TaskId = taskId,
            AuthorUserId = authorUserId,
            Body = body
        });
    }
}
