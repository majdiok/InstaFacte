using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Résout le nombre de parents à charge effectif pour le calcul IRPP, et détecte
/// les conflits de non-cumul (même CIN parent déclaré par deux salariés du tenant).
/// Couche pure en amont de <see cref="PayrollCalculator"/> — ne modifie pas la formule.
/// </summary>
public static class ParentDeductionEligibilityResolver
{
    public const string IncompleteWarningCode = "ParentClaimsIncomplete";
    public const string ConflictErrorCode = "ParentClaimConflict";

    /// <summary>
    /// Résout l'éligibilité pour un salarié donné.
    /// </summary>
    /// <param name="legacyDependentParentsCount">Compteur synchronisé / legacy sur la fiche.</param>
    /// <param name="activeClaimsForEmployee">Déclarations actives du salarié.</param>
    /// <param name="activeCinOwners">Index CIN parent actif → EmployeeId déclarant.</param>
    /// <param name="employeeId">Salarié évalué.</param>
    public static ParentEligibilityResult ResolveForEmployee(
        int legacyDependentParentsCount,
        IReadOnlyList<EmployeeDependentParent> activeClaimsForEmployee,
        IReadOnlyDictionary<string, Guid> activeCinOwners,
        Guid employeeId)
    {
        foreach (var claim in activeClaimsForEmployee)
        {
            if (activeCinOwners.TryGetValue(claim.ParentCin, out var ownerId) && ownerId != employeeId)
            {
                return new ParentEligibilityResult
                {
                    EffectiveDependentParents = 0,
                    Status = ParentClaimsStatus.Conflict,
                    WarningCode = ConflictErrorCode,
                    WarningMessage =
                        $"Le parent CIN {claim.ParentCin} est déjà déclaré par un autre salarié de l'entreprise (non-cumul art. 40 IRPP)."
                };
            }
        }

        if (activeClaimsForEmployee.Count > 0)
        {
            return new ParentEligibilityResult
            {
                EffectiveDependentParents = Math.Clamp(activeClaimsForEmployee.Count, 0, 2),
                Status = ParentClaimsStatus.Complete
            };
        }

        if (legacyDependentParentsCount > 0)
        {
            return new ParentEligibilityResult
            {
                EffectiveDependentParents = 0,
                Status = ParentClaimsStatus.Incomplete,
                WarningCode = IncompleteWarningCode,
                WarningMessage =
                    "Parents à charge : CIN non renseignés — la déduction parent n'est pas appliquée tant que les déclarations nominatives ne sont pas complétées."
            };
        }

        return new ParentEligibilityResult
        {
            EffectiveDependentParents = 0,
            Status = ParentClaimsStatus.None
        };
    }

    /// <summary>
    /// Détecte les CIN parents actifs déclarés par plus d'un salarié.
    /// </summary>
    public static IReadOnlyList<ParentClaimConflict> FindConflicts(
        IReadOnlyList<EmployeeDependentParent> allActiveClaims)
    {
        return allActiveClaims
            .GroupBy(c => c.ParentCin, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.EmployeeId).Distinct().Count() > 1)
            .Select(g =>
            {
                var owners = g.Select(x => x.EmployeeId).Distinct().Take(2).ToList();
                return new ParentClaimConflict(g.Key, owners[0], owners[1]);
            })
            .OrderBy(c => c.ParentCin)
            .ToList();
    }

    /// <summary>
    /// Bloque le calcul de paie si des conflits de CIN parent existent dans le tenant.
    /// </summary>
    public static Result ValidateNoConflicts(
        IReadOnlyList<EmployeeDependentParent> allActiveClaims,
        IReadOnlyDictionary<Guid, string>? employeeNames = null)
    {
        var conflicts = FindConflicts(allActiveClaims);
        if (conflicts.Count == 0)
            return Result.Success();

        var details = conflicts.Select(c =>
        {
            var nameA = employeeNames?.GetValueOrDefault(c.EmployeeIdA) ?? c.EmployeeIdA.ToString("N")[..8];
            var nameB = employeeNames?.GetValueOrDefault(c.EmployeeIdB) ?? c.EmployeeIdB.ToString("N")[..8];
            return $"CIN {c.ParentCin} : {nameA} et {nameB}";
        });

        return Result.Failure(Error.Conflict(
            $"Non-cumul parents à charge : le même parent est déclaré par plusieurs salariés ({string.Join(" ; ", details)}). Corrigez les fiches avant de calculer la paie.",
            new Dictionary<string, object?>
            {
                ["code"] = ConflictErrorCode,
                ["conflicts"] = conflicts.Select(c => new
                {
                    c.ParentCin,
                    c.EmployeeIdA,
                    c.EmployeeIdB
                }).ToList()
            }));
    }

    public static string ResolveStatusLabel(ParentClaimsStatus status) => status switch
    {
        ParentClaimsStatus.None => "None",
        ParentClaimsStatus.Complete => "Complete",
        ParentClaimsStatus.Incomplete => "Incomplete",
        ParentClaimsStatus.Conflict => "Conflict",
        _ => status.ToString()
    };
}

/// <summary>Résultat d'éligibilité parents à charge pour un salarié.</summary>
public sealed class ParentEligibilityResult
{
    public int EffectiveDependentParents { get; init; }
    public ParentClaimsStatus Status { get; init; }
    public string? WarningCode { get; init; }
    public string? WarningMessage { get; init; }
}

/// <summary>Conflit de non-cumul : même CIN parent, deux déclarants.</summary>
public sealed record ParentClaimConflict(string ParentCin, Guid EmployeeIdA, Guid EmployeeIdB);
