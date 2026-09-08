using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectMember : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectMemberRole Role { get; private set; }
    public decimal? SalesRate { get; private set; }
    public decimal? HourlyCost { get; private set; }
    public decimal WeeklyCapacityHours { get; private set; }

    private ProjectMember() { }

    public static Result<ProjectMember> Create(
        Guid projectId,
        Guid userId,
        ProjectMemberRole role,
        decimal? salesRate,
        decimal? hourlyCost,
        decimal weeklyCapacityHours)
    {
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectMember>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (userId == Guid.Empty)
            return Result.Failure<ProjectMember>(Error.Validation("UserId", "L'utilisateur est obligatoire"));
        if (salesRate is < 0)
            return Result.Failure<ProjectMember>(Error.Validation("SalesRate", "Le tarif de vente ne peut pas être négatif"));
        if (hourlyCost is < 0)
            return Result.Failure<ProjectMember>(Error.Validation("HourlyCost", "Le coût horaire ne peut pas être négatif"));
        if (weeklyCapacityHours < 0)
            return Result.Failure<ProjectMember>(Error.Validation("WeeklyCapacityHours", "La capacité hebdomadaire ne peut pas être négative"));

        return Result.Success(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            SalesRate = salesRate,
            HourlyCost = hourlyCost,
            WeeklyCapacityHours = decimal.Round(weeklyCapacityHours, 2)
        });
    }

    public Result Update(ProjectMemberRole role, decimal? salesRate, decimal? hourlyCost, decimal weeklyCapacityHours)
    {
        if (salesRate is < 0)
            return Result.Failure(Error.Validation("SalesRate", "Le tarif de vente ne peut pas être négatif"));
        if (hourlyCost is < 0)
            return Result.Failure(Error.Validation("HourlyCost", "Le coût horaire ne peut pas être négatif"));
        if (weeklyCapacityHours < 0)
            return Result.Failure(Error.Validation("WeeklyCapacityHours", "La capacité hebdomadaire ne peut pas être négative"));

        Role = role;
        SalesRate = salesRate;
        HourlyCost = hourlyCost;
        WeeklyCapacityHours = decimal.Round(weeklyCapacityHours, 2);
        return Result.Success();
    }
}
