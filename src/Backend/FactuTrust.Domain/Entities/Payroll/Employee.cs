using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Dossier d'un salarié de l'entreprise (module Paie).
/// </summary>
public sealed class Employee : AggregateRoot
{
    public string EmployeeNumber { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    /// <summary>Numéro de carte d'identité nationale (CIN).</summary>
    public string? Cin { get; private set; }
    /// <summary>Matricule / numéro d'affiliation CNSS du salarié.</summary>
    public string? CnssNumber { get; private set; }

    public DateTime? DateOfBirth { get; private set; }
    public DateTime HireDate { get; private set; }
    /// <summary>Date de sortie (fin de contrat / départ). Null si toujours en poste.</summary>
    public DateTime? TerminationDate { get; private set; }

    public MaritalStatus MaritalStatus { get; private set; }
    /// <summary>Le salarié est-il chef de famille (déduction fiscale correspondante) ?</summary>
    public bool IsHeadOfFamily { get; private set; }
    /// <summary>Nombre d'enfants à charge.</summary>
    public int DependentChildren { get; private set; }

    public Address? Address { get; private set; }
    public Email? Email { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    /// <summary>RIB bancaire pour le virement du salaire.</summary>
    public string? Rib { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Solde initial de congés payés (reprise historique), en jours.</summary>
    public decimal LeaveOpeningBalanceDays { get; private set; }

    private readonly List<EmploymentContract> _contracts = new();
    public IReadOnlyCollection<EmploymentContract> Contracts => _contracts.AsReadOnly();

    private Employee() { }

    public static Result<Employee> Create(
        string employeeNumber,
        string firstName,
        string lastName,
        DateTime hireDate,
        MaritalStatus maritalStatus = MaritalStatus.Single,
        bool isHeadOfFamily = false,
        int dependentChildren = 0,
        string? cin = null,
        string? cnssNumber = null,
        DateTime? dateOfBirth = null,
        Address? address = null,
        Email? email = null,
        PhoneNumber? phone = null,
        string? rib = null)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber))
            return Result.Failure<Employee>(Error.Validation("EmployeeNumber", "Le matricule du salarié est obligatoire."));
        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure<Employee>(Error.Validation("FirstName", "Le prénom est obligatoire."));
        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure<Employee>(Error.Validation("LastName", "Le nom est obligatoire."));
        if (hireDate == default)
            return Result.Failure<Employee>(Error.Validation("HireDate", "La date d'embauche est obligatoire."));
        if (dependentChildren < 0)
            return Result.Failure<Employee>(Error.Validation("DependentChildren", "Le nombre d'enfants à charge ne peut pas être négatif."));

        return Result.Success(new Employee
        {
            EmployeeNumber = employeeNumber.Trim(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            HireDate = hireDate.Date,
            MaritalStatus = maritalStatus,
            IsHeadOfFamily = isHeadOfFamily,
            DependentChildren = dependentChildren,
            Cin = Normalize(cin),
            CnssNumber = Normalize(cnssNumber),
            DateOfBirth = dateOfBirth?.Date,
            Address = address,
            Email = email,
            Phone = phone,
            Rib = Normalize(rib),
            IsActive = true,
            LeaveOpeningBalanceDays = 0m
        });
    }

    public Result SetLeaveOpeningBalance(decimal days)
    {
        if (days < 0)
            return Result.Failure(Error.Validation("LeaveOpeningBalanceDays", "Le solde initial ne peut pas être négatif."));

        LeaveOpeningBalanceDays = Math.Round(days, 3);
        IncrementVersion();
        return Result.Success();
    }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public Result Update(
        string firstName,
        string lastName,
        MaritalStatus maritalStatus,
        bool isHeadOfFamily,
        int dependentChildren,
        string? cin,
        string? cnssNumber,
        DateTime? dateOfBirth,
        Address? address,
        Email? email,
        PhoneNumber? phone,
        string? rib)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return Result.Failure(Error.Validation("FirstName", "Le prénom est obligatoire."));
        if (string.IsNullOrWhiteSpace(lastName))
            return Result.Failure(Error.Validation("LastName", "Le nom est obligatoire."));
        if (dependentChildren < 0)
            return Result.Failure(Error.Validation("DependentChildren", "Le nombre d'enfants à charge ne peut pas être négatif."));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        MaritalStatus = maritalStatus;
        IsHeadOfFamily = isHeadOfFamily;
        DependentChildren = dependentChildren;
        Cin = Normalize(cin);
        CnssNumber = Normalize(cnssNumber);
        DateOfBirth = dateOfBirth?.Date;
        Address = address;
        Email = email;
        Phone = phone;
        Rib = Normalize(rib);
        IncrementVersion();
        return Result.Success();
    }

    public Result<EmploymentContract> AddContract(
        ContractType type,
        SocialRegime regime,
        DateTime startDate,
        decimal baseSalary,
        decimal workAccidentRate,
        DateTime? endDate = null,
        string? jobTitle = null)
    {
        var contractResult = EmploymentContract.Create(Id, type, regime, startDate, baseSalary, workAccidentRate, endDate, jobTitle);
        if (contractResult.IsFailure)
            return contractResult;

        _contracts.Add(contractResult.Value);
        IncrementVersion();
        return contractResult;
    }

    /// <summary>Contrat actif à la date donnée (dernier contrat couvrant la date).</summary>
    public EmploymentContract? GetActiveContract(DateTime date)
    {
        return _contracts
            .Where(c => c.StartDate.Date <= date.Date && (c.EndDate == null || c.EndDate.Value.Date >= date.Date))
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefault();
    }

    public void Terminate(DateTime terminationDate)
    {
        TerminationDate = terminationDate.Date;
        IsActive = false;
        IncrementVersion();
    }

    public void Deactivate()
    {
        IsActive = false;
        IncrementVersion();
    }

    public void Reactivate()
    {
        IsActive = true;
        TerminationDate = null;
        IncrementVersion();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
