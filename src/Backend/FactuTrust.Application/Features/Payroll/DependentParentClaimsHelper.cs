using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Payroll;

/// <summary>
/// Construction et validation des déclarations nominatives de parents à charge.
/// </summary>
public static class DependentParentClaimsHelper
{
    /// <summary>
    /// Si <paramref name="claims"/> est fourni (y compris liste vide), il fait autorité.
    /// Sinon, conserve le compteur legacy <paramref name="legacyDependentParents"/> sans créer de lignes CIN.
    /// </summary>
    public static bool ClaimsProvided(IReadOnlyList<DependentParentClaimDto>? claims) =>
        claims is not null;

    public static Result<(IReadOnlyList<EmployeeDependentParent> Entities, int SyncCount)> BuildClaims(
        Guid employeeId,
        string? employeeCin,
        IReadOnlyList<DependentParentClaimDto> claims,
        DateTime startDate)
    {
        if (claims.Count > 2)
            return Result.Failure<(IReadOnlyList<EmployeeDependentParent>, int)>(
                Error.Validation("DependentParentClaims", "Un salarié ne peut déclarer que 2 parents à charge au maximum."));

        var entities = new List<EmployeeDependentParent>(claims.Count);
        foreach (var dto in claims)
        {
            if (string.IsNullOrWhiteSpace(dto.ParentCin) || !TunisianValidationRules.IsValidCin(dto.ParentCin))
                return Result.Failure<(IReadOnlyList<EmployeeDependentParent>, int)>(
                    Error.Validation("ParentCin", "Le CIN du parent est invalide (8 chiffres attendus)."));

            if (!Enum.TryParse<DependentParentKinship>(dto.Kinship, ignoreCase: true, out var kinship)
                || kinship is not (DependentParentKinship.Father or DependentParentKinship.Mother))
            {
                return Result.Failure<(IReadOnlyList<EmployeeDependentParent>, int)>(
                    Error.Validation("Kinship", "Le lien de parenté doit être Father ou Mother."));
            }

            var created = EmployeeDependentParent.Create(
                employeeId,
                dto.ParentCin,
                kinship,
                startDate,
                dto.FirstName,
                dto.LastName,
                employeeCin);

            if (created.IsFailure)
                return Result.Failure<(IReadOnlyList<EmployeeDependentParent>, int)>(created.Error);

            entities.Add(created.Value);
        }

        var setResult = EmployeeDependentParent.ValidateSet(entities);
        if (setResult.IsFailure)
            return Result.Failure<(IReadOnlyList<EmployeeDependentParent>, int)>(setResult.Error);

        return Result.Success<(IReadOnlyList<EmployeeDependentParent>, int)>((entities, entities.Count));
    }

    public static async Task<Result> EnsureNoConflictsAsync(
        IEmployeeDependentParentRepository repository,
        IEmployeeRepository employees,
        IReadOnlyList<EmployeeDependentParent> claims,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        foreach (var claim in claims)
        {
            var conflict = await repository.FindActiveConflictAsync(claim.ParentCin, employeeId, cancellationToken);
            if (conflict is null)
                continue;

            var other = await employees.GetByIdAsync(conflict.EmployeeId, cancellationToken);
            var otherName = other?.FullName ?? conflict.EmployeeId.ToString("N")[..8];
            return Result.Failure(Error.Conflict(
                $"Le parent CIN {claim.ParentCin} est déjà déclaré par {otherName}. Un parent ne peut être à charge que d'un seul salarié de l'entreprise (art. 40 IRPP).",
                new Dictionary<string, object?>
                {
                    ["code"] = Domain.Services.Payroll.ParentDeductionEligibilityResolver.ConflictErrorCode,
                    ["parentCin"] = claim.ParentCin,
                    ["conflictingEmployeeId"] = conflict.EmployeeId,
                    ["conflictingEmployeeName"] = otherName
                }));
        }

        return Result.Success();
    }

    public static DependentParentClaimDto ToDto(EmployeeDependentParent p) => new()
    {
        Id = p.Id,
        ParentCin = p.ParentCin,
        Kinship = p.Kinship.ToString(),
        FirstName = p.FirstName,
        LastName = p.LastName
    };
}
