using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Contrat de travail d'un salarié (module Paie).
/// </summary>
public sealed class EmploymentContract : Entity
{
    public Guid EmployeeId { get; private set; }
    public ContractType Type { get; private set; }
    public SocialRegime Regime { get; private set; }
    /// <summary>Régime de durée hebdomadaire (48 h ou 40 h) — pilote le calcul des heures supplémentaires.</summary>
    public WeeklyWorkRegime WeeklyRegime { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    /// <summary>Salaire de base mensuel brut (TND).</summary>
    public decimal BaseSalary { get; private set; }
    /// <summary>Taux d'accident de travail applicable (charge patronale), en %. Ex. 0.4 à 4.</summary>
    public decimal WorkAccidentRate { get; private set; }
    public string? JobTitle { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Date de début de la convention CIVP (SIVP).</summary>
    public DateTime? CivpStartDate { get; private set; }
    /// <summary>Date de fin de la convention CIVP (SIVP).</summary>
    public DateTime? CivpEndDate { get; private set; }
    /// <summary>Subvention État (ANETI) mensuelle.</summary>
    public decimal CivpStateGrant { get; private set; }
    /// <summary>Indemnité complémentaire employeur.</summary>
    public decimal CivpEmployerAllowance { get; private set; }
    /// <summary>Référence dossier ANETI.</summary>
    public string? AnetiReference { get; private set; }

    private readonly List<ContractAllowance> _allowances = new();
    /// <summary>Primes et indemnités récurrentes du contrat.</summary>
    public IReadOnlyCollection<ContractAllowance> Allowances => _allowances.AsReadOnly();

    private EmploymentContract() { }

    /// <summary>
    /// Crée un contrat rattaché à un salarié existant (utilisé par la couche application
    /// lorsque l'agrégat Employee n'est pas chargé en mémoire).
    /// </summary>
    public static Result<EmploymentContract> CreatePublic(
        Guid employeeId,
        ContractType type,
        SocialRegime regime,
        DateTime startDate,
        decimal baseSalary,
        decimal workAccidentRate,
        DateTime? endDate = null,
        string? jobTitle = null,
        WeeklyWorkRegime weeklyRegime = WeeklyWorkRegime.FortyEightHours,
        DateTime? civpStartDate = null,
        DateTime? civpEndDate = null,
        decimal civpStateGrant = 0m,
        decimal civpEmployerAllowance = 0m,
        string? anetiReference = null)
        => Create(employeeId, type, regime, startDate, baseSalary, workAccidentRate, endDate, jobTitle, weeklyRegime,
            civpStartDate, civpEndDate, civpStateGrant, civpEmployerAllowance, anetiReference);

    internal static Result<EmploymentContract> Create(
        Guid employeeId,
        ContractType type,
        SocialRegime regime,
        DateTime startDate,
        decimal baseSalary,
        decimal workAccidentRate,
        DateTime? endDate = null,
        string? jobTitle = null,
        WeeklyWorkRegime weeklyRegime = WeeklyWorkRegime.FortyEightHours,
        DateTime? civpStartDate = null,
        DateTime? civpEndDate = null,
        decimal civpStateGrant = 0m,
        decimal civpEmployerAllowance = 0m,
        string? anetiReference = null)
    {
        if (startDate == default)
            return Result.Failure<EmploymentContract>(Error.Validation("StartDate", "La date de début du contrat est obligatoire."));
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure<EmploymentContract>(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));
        if (baseSalary < 0)
            return Result.Failure<EmploymentContract>(Error.Validation("BaseSalary", "Le salaire de base ne peut pas être négatif."));
        if (workAccidentRate < 0 || workAccidentRate > 100)
            return Result.Failure<EmploymentContract>(Error.Validation("WorkAccidentRate", "Le taux d'accident de travail doit être compris entre 0 et 100 %."));

        return Result.Success(new EmploymentContract
        {
            EmployeeId = employeeId,
            Type = type,
            Regime = regime,
            WeeklyRegime = weeklyRegime,
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            BaseSalary = Math.Round(baseSalary, 3),
            WorkAccidentRate = Math.Round(workAccidentRate, 3),
            JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim(),
            IsActive = true,
            CivpStartDate = civpStartDate?.Date,
            CivpEndDate = civpEndDate?.Date,
            CivpStateGrant = Math.Round(civpStateGrant, 3),
            CivpEmployerAllowance = Math.Round(civpEmployerAllowance, 3),
            AnetiReference = string.IsNullOrWhiteSpace(anetiReference) ? null : anetiReference.Trim()
        });
    }

    public Result Update(
        ContractType type,
        SocialRegime regime,
        DateTime startDate,
        decimal baseSalary,
        decimal workAccidentRate,
        DateTime? endDate,
        string? jobTitle,
        bool isActive,
        WeeklyWorkRegime weeklyRegime = WeeklyWorkRegime.FortyEightHours,
        DateTime? civpStartDate = null,
        DateTime? civpEndDate = null,
        decimal civpStateGrant = 0m,
        decimal civpEmployerAllowance = 0m,
        string? anetiReference = null)
    {
        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
            return Result.Failure(Error.Validation("EndDate", "La date de fin ne peut pas être antérieure à la date de début."));
        if (baseSalary < 0)
            return Result.Failure(Error.Validation("BaseSalary", "Le salaire de base ne peut pas être négatif."));
        if (workAccidentRate < 0 || workAccidentRate > 100)
            return Result.Failure(Error.Validation("WorkAccidentRate", "Le taux d'accident de travail doit être compris entre 0 et 100 %."));

        Type = type;
        Regime = regime;
        WeeklyRegime = weeklyRegime;
        StartDate = startDate.Date;
        EndDate = endDate?.Date;
        BaseSalary = Math.Round(baseSalary, 3);
        WorkAccidentRate = Math.Round(workAccidentRate, 3);
        JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        IsActive = isActive;
        CivpStartDate = civpStartDate?.Date;
        CivpEndDate = civpEndDate?.Date;
        CivpStateGrant = Math.Round(civpStateGrant, 3);
        CivpEmployerAllowance = Math.Round(civpEmployerAllowance, 3);
        AnetiReference = string.IsNullOrWhiteSpace(anetiReference) ? null : anetiReference.Trim();
        return Result.Success();
    }

    public Result AddAllowance(string label, decimal amount, bool taxable, bool subjectToCnss)
    {
        var allowanceResult = ContractAllowance.Create(Id, label, amount, taxable, subjectToCnss);
        if (allowanceResult.IsFailure)
            return allowanceResult;
        _allowances.Add(allowanceResult.Value);
        return Result.Success();
    }

    public void ClearAllowances() => _allowances.Clear();
}

/// <summary>
/// Prime / indemnité récurrente attachée à un contrat.
/// </summary>
public sealed class ContractAllowance : Entity
{
    public Guid ContractId { get; private set; }
    public string Label { get; private set; } = null!;
    public decimal Amount { get; private set; }
    /// <summary>La prime est-elle imposable à l'IRPP ?</summary>
    public bool Taxable { get; private set; }
    /// <summary>La prime est-elle soumise à cotisation CNSS ?</summary>
    public bool SubjectToCnss { get; private set; }

    private ContractAllowance() { }

    internal static Result<ContractAllowance> Create(Guid contractId, string label, decimal amount, bool taxable, bool subjectToCnss)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<ContractAllowance>(Error.Validation("Label", "Le libellé de la prime est obligatoire."));
        if (amount < 0)
            return Result.Failure<ContractAllowance>(Error.Validation("Amount", "Le montant de la prime ne peut pas être négatif."));

        return Result.Success(new ContractAllowance
        {
            ContractId = contractId,
            Label = label.Trim(),
            Amount = Math.Round(amount, 3),
            Taxable = taxable,
            SubjectToCnss = subjectToCnss
        });
    }
}
