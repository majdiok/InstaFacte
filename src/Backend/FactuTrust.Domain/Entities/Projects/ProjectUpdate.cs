using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

/// <summary>Periodic project status snapshot (Odoo Project updates).</summary>
public sealed class ProjectUpdate : Entity
{
    public Guid ProjectId { get; private set; }
    public ProjectUpdateStatus Status { get; private set; }
    public int ProgressPercent { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public DateTime UpdateDate { get; private set; }
    public string? Description { get; private set; }

    private ProjectUpdate() { }

    public static Result<ProjectUpdate> Create(
        Guid projectId,
        Guid authorUserId,
        ProjectUpdateStatus status,
        int progressPercent,
        string? description)
    {
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectUpdate>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (authorUserId == Guid.Empty)
            return Result.Failure<ProjectUpdate>(Error.Validation("AuthorUserId", "L'auteur est obligatoire"));
        if (progressPercent is < 0 or > 100)
            return Result.Failure<ProjectUpdate>(Error.Validation("ProgressPercent", "La progression doit être entre 0 et 100"));

        return Result.Success(new ProjectUpdate
        {
            ProjectId = projectId,
            AuthorUserId = authorUserId,
            Status = status,
            ProgressPercent = progressPercent,
            UpdateDate = DateTime.UtcNow,
            Description = description?.Trim()
        });
    }
}
