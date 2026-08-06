using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Déclaration nominative d'un parent à charge (art. 40 IRPP).
/// Un même CIN parent ne peut être actif que pour un seul salarié du tenant.
/// </summary>
public sealed class EmployeeDependentParent : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    /// <summary>CIN du parent (8 chiffres, normalisé).</summary>
    public string ParentCin { get; private set; } = null!;
    public DependentParentKinship Kinship { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public DateTime StartDate { get; private set; }
    /// <summary>Null = déclaration active. Renseigné = CIN libéré pour un autre déclarant.</summary>
    public DateTime? EndDate { get; private set; }

    public bool IsActive => EndDate is null;

    private EmployeeDependentParent() { }

    public static Result<EmployeeDependentParent> Create(
        Guid employeeId,
        string parentCin,
        DependentParentKinship kinship,
        DateTime startDate,
        string? firstName = null,
        string? lastName = null,
        string? employeeCin = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeeDependentParent>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));

        var normalizedCin = NormalizeCin(parentCin);
        if (normalizedCin is null)
            return Result.Failure<EmployeeDependentParent>(Error.Validation("ParentCin", "Le CIN du parent est obligatoire (8 chiffres)."));

        var normalizedEmployeeCin = NormalizeCin(employeeCin);
        if (normalizedEmployeeCin is not null && normalizedEmployeeCin == normalizedCin)
            return Result.Failure<EmployeeDependentParent>(Error.Validation("ParentCin", "Le CIN du parent ne peut pas être identique à celui du salarié."));

        if (kinship is not (DependentParentKinship.Father or DependentParentKinship.Mother))
            return Result.Failure<EmployeeDependentParent>(Error.Validation("Kinship", "Le lien de parenté doit être Père ou Mère."));

        return Result.Success(new EmployeeDependentParent
        {
            EmployeeId = employeeId,
            ParentCin = normalizedCin,
            Kinship = kinship,
            FirstName = NormalizeName(firstName),
            LastName = NormalizeName(lastName),
            StartDate = startDate.Date,
            EndDate = null
        });
    }

    public Result End(DateTime endDate)
    {
        if (EndDate.HasValue)
            return Result.Failure(Error.Validation("EndDate", "Cette déclaration de parent à charge est déjà clôturée."));

        var date = endDate.Date;
        if (date < StartDate.Date)
            return Result.Failure(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));

        EndDate = date;
        IncrementVersion();
        return Result.Success();
    }

    /// <summary>Valide un jeu de déclarations pour un même salarié (max 2, CIN et parenté uniques).</summary>
    public static Result ValidateSet(IReadOnlyList<EmployeeDependentParent> claims)
    {
        if (claims.Count > 2)
            return Result.Failure(Error.Validation("DependentParentClaims", "Un salarié ne peut déclarer que 2 parents à charge au maximum."));

        var cins = new HashSet<string>(StringComparer.Ordinal);
        var kinships = new HashSet<DependentParentKinship>();
        foreach (var claim in claims)
        {
            if (!cins.Add(claim.ParentCin))
                return Result.Failure(Error.Validation("ParentCin", $"Le CIN parent {claim.ParentCin} est déclaré en double sur la fiche."));
            if (!kinships.Add(claim.Kinship))
                return Result.Failure(Error.Validation("Kinship", "Un seul père et une seule mère peuvent être déclarés."));
        }

        return Result.Success();
    }

    public static string? NormalizeCin(string? cin)
    {
        if (string.IsNullOrWhiteSpace(cin))
            return null;

        var cleaned = cin.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (cleaned.Length != 8 || !cleaned.All(char.IsDigit))
            return null;

        return cleaned;
    }

    private static string? NormalizeName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
