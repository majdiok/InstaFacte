using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;

namespace FactuTrust.Infrastructure.Services.SectorRules;

/// <summary>
/// Plan §2.2 — validation rules for <c>CompanyModulesController.UpdateModules</c>, extracted into a
/// standalone static class so they are unit-testable without the API assembly (the test project
/// only references Domain/Application/Infrastructure).
/// </summary>
public static class CompanyModuleReconfigurationValidator
{
    /// <summary>
    /// Core modules are always enabled and can never be disabled. Returns a French error message
    /// listing every missing core module, or null when the request already includes all of them.
    /// </summary>
    public static string? ValidateCoreModules(HashSet<AppModule> requestedSet, IReadOnlyList<AppModule> coreModules)
    {
        var missing = coreModules.Where(m => !requestedSet.Contains(m)).ToList();
        if (missing.Count == 0)
            return null;

        var labels = string.Join(", ", missing.Select(m => m.ToDisplayString()));
        return $"Les modules suivants sont obligatoires et ne peuvent pas être désactivés : {labels}.";
    }

    /// <summary>
    /// A module required by another still-requested module cannot be disabled. Returns the French
    /// "Requis par X" error message for the first violation found, or null when the request is
    /// dependency-consistent. Edges with an unrecognized module id (defensive — should not happen
    /// with a validated catalog) are skipped.
    /// </summary>
    public static string? ValidateDependencies(HashSet<AppModule> requestedSet, IReadOnlyList<ModuleDependencySnapshot> dependencyEdges)
    {
        foreach (var edge in dependencyEdges)
        {
            if (!Enum.IsDefined(typeof(AppModule), edge.ModuleId) || !Enum.IsDefined(typeof(AppModule), edge.RequiredModuleId))
                continue;

            var dependent = (AppModule)edge.ModuleId;
            var required = (AppModule)edge.RequiredModuleId;
            if (requestedSet.Contains(dependent) && !requestedSet.Contains(required))
            {
                return $"Requis par « {dependent.ToDisplayString()} » : le module « {required.ToDisplayString()} » ne peut pas être désactivé.";
            }
        }

        return null;
    }
}
