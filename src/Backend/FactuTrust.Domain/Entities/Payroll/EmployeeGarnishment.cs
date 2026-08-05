using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>Saisie sur salaire ou pension alimentaire.</summary>
public sealed class EmployeeGarnishment : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public GarnishmentType Type { get; private set; }
    public string Reference { get; private set; } = null!;
    public DateTime IssuedAt { get; private set; }
    public string BeneficiaryName { get; private set; } = null!;
    public string? BeneficiaryRib { get; private set; }
    public int Priority { get; private set; }
    public GarnishmentAmountKind Kind { get; private set; }
    public decimal? FixedAmount { get; private set; }
    public decimal? PercentOfNet { get; private set; }
    public decimal? TotalAmountDue { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public EmployeeGarnishmentStatus Status { get; private set; }

    private readonly List<EmployeeGarnishmentInstallment> _installments = new();
    public IReadOnlyCollection<EmployeeGarnishmentInstallment> Installments => _installments.AsReadOnly();

    public decimal TotalApplied => R(_installments.Sum(i => i.AppliedAmount));

    private EmployeeGarnishment() { }

    public static Result<EmployeeGarnishment> Create(
        Guid employeeId,
        GarnishmentType type,
        string reference,
        DateTime issuedAt,
        string beneficiaryName,
        string? beneficiaryRib,
        int priority,
        GarnishmentAmountKind kind,
        decimal? fixedAmount,
        decimal? percentOfNet,
        decimal? totalAmountDue,
        DateTime startDate,
        DateTime? endDate = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeeGarnishment>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (string.IsNullOrWhiteSpace(reference))
            return Result.Failure<EmployeeGarnishment>(Error.Validation("Reference", "La référence est obligatoire."));
        if (string.IsNullOrWhiteSpace(beneficiaryName))
            return Result.Failure<EmployeeGarnishment>(Error.Validation("BeneficiaryName", "Le bénéficiaire est obligatoire."));
        if (kind == GarnishmentAmountKind.FixedAmount && (!fixedAmount.HasValue || fixedAmount <= 0))
            return Result.Failure<EmployeeGarnishment>(Error.Validation("FixedAmount", "Le montant fixe est obligatoire."));
        if (kind == GarnishmentAmountKind.PercentOfNet && (!percentOfNet.HasValue || percentOfNet <= 0))
            return Result.Failure<EmployeeGarnishment>(Error.Validation("PercentOfNet", "Le pourcentage est obligatoire."));

        return Result.Success(new EmployeeGarnishment
        {
            EmployeeId = employeeId,
            Type = type,
            Reference = reference.Trim(),
            IssuedAt = issuedAt.Date,
            BeneficiaryName = beneficiaryName.Trim(),
            BeneficiaryRib = string.IsNullOrWhiteSpace(beneficiaryRib) ? null : beneficiaryRib.Trim(),
            Priority = priority,
            Kind = kind,
            FixedAmount = fixedAmount.HasValue ? R(fixedAmount.Value) : null,
            PercentOfNet = percentOfNet,
            TotalAmountDue = totalAmountDue.HasValue ? R(totalAmountDue.Value) : null,
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            Status = EmployeeGarnishmentStatus.Active
        });
    }

    public bool IsActiveOn(DateTime date)
    {
        if (Status != EmployeeGarnishmentStatus.Active) return false;
        if (date.Date < StartDate.Date) return false;
        if (EndDate.HasValue && date.Date > EndDate.Value.Date) return false;
        if (TotalAmountDue.HasValue && TotalApplied >= TotalAmountDue.Value) return false;
        return true;
    }

    public decimal ComputeRequestedAmount(decimal netBeforeGarnishments)
    {
        var amount = Kind switch
        {
            GarnishmentAmountKind.FixedAmount => FixedAmount ?? 0m,
            GarnishmentAmountKind.PercentOfNet => R(netBeforeGarnishments * (PercentOfNet ?? 0m) / 100m),
            _ => 0m
        };

        if (TotalAmountDue.HasValue)
            amount = Math.Min(amount, R(TotalAmountDue.Value - TotalApplied));

        return R(Math.Max(0m, amount));
    }

    public void RecordInstallment(int year, int month, Guid payrollRunId, decimal requested, decimal applied, decimal carriedOver)
    {
        _installments.Add(EmployeeGarnishmentInstallment.Create(
            Id, year, month, payrollRunId, requested, applied, carriedOver));

        if (TotalAmountDue.HasValue && TotalApplied >= TotalAmountDue.Value)
            Status = EmployeeGarnishmentStatus.Completed;

        IncrementVersion();
    }

    public void RemoveInstallmentsForRun(Guid payrollRunId)
    {
        _installments.RemoveAll(i => i.PayrollRunId == payrollRunId);
        if (Status == EmployeeGarnishmentStatus.Completed)
            Status = EmployeeGarnishmentStatus.Active;
        IncrementVersion();
    }

    public void Cancel()
    {
        Status = EmployeeGarnishmentStatus.Cancelled;
        IncrementVersion();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed class EmployeeGarnishmentInstallment : Entity
{
    public Guid EmployeeGarnishmentId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public Guid PayrollRunId { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public decimal AppliedAmount { get; private set; }
    public decimal CarriedOverAmount { get; private set; }

    private EmployeeGarnishmentInstallment() { }

    internal static EmployeeGarnishmentInstallment Create(
        Guid garnishmentId, int year, int month, Guid payrollRunId,
        decimal requested, decimal applied, decimal carriedOver) =>
        new()
        {
            EmployeeGarnishmentId = garnishmentId,
            Year = year,
            Month = month,
            PayrollRunId = payrollRunId,
            RequestedAmount = Math.Round(requested, 3, MidpointRounding.AwayFromZero),
            AppliedAmount = Math.Round(applied, 3, MidpointRounding.AwayFromZero),
            CarriedOverAmount = Math.Round(carriedOver, 3, MidpointRounding.AwayFromZero)
        };
}
