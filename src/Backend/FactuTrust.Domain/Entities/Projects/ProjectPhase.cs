using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Projects;

public sealed class ProjectPhase : Entity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public string? Color { get; private set; }

    private ProjectPhase() { }

    public static Result<ProjectPhase> Create(Guid projectId, string name, int sortOrder, string? color = null)
    {
        name = name?.Trim() ?? string.Empty;
        if (projectId == Guid.Empty)
            return Result.Failure<ProjectPhase>(Error.Validation("ProjectId", "Le projet est obligatoire"));
        if (string.IsNullOrEmpty(name))
            return Result.Failure<ProjectPhase>(Error.Validation("Name", "Le nom de la colonne est obligatoire"));
        if (name.Length > 80)
            name = name[..80];

        return Result.Success(new ProjectPhase
        {
            ProjectId = projectId,
            Name = name,
            SortOrder = sortOrder,
            Color = color?.Trim()
        });
    }

    public Result Rename(string name, string? color)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return Result.Failure(Error.Validation("Name", "Le nom de la colonne est obligatoire"));
        Name = name.Length > 80 ? name[..80] : name;
        Color = color?.Trim();
        return Result.Success();
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    public static IReadOnlyList<(string Name, int Order, string Color)> DefaultColumns() =>
        DefaultColumnsFor(ProjectKind.Generic);

    public static IReadOnlyList<(string Name, int Order, string Color)> DefaultColumnsFor(ProjectKind kind) =>
        kind switch
        {
            ProjectKind.Esn => new (string, int, string)[]
            {
                ("Backlog", 0, "#64748b"),
                ("Spécifications", 1, "#7c3aed"),
                ("Réalisation", 2, "#2563eb"),
                ("Recette", 3, "#d97706"),
                ("Livré", 4, "#16a34a")
            },
            ProjectKind.Btp => new (string, int, string)[]
            {
                ("Préparation", 0, "#64748b"),
                ("Exécution", 1, "#2563eb"),
                ("Contrôle", 2, "#d97706"),
                ("Réception", 3, "#7c3aed"),
                ("Clôturé", 4, "#16a34a")
            },
            _ => new (string, int, string)[]
            {
                ("À faire", 0, "#64748b"),
                ("En cours", 1, "#2563eb"),
                ("En attente", 2, "#d97706"),
                ("Terminé", 3, "#16a34a"),
                ("Annulé", 4, "#dc2626")
            }
        };
}
