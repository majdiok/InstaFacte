using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Avance sur salaire consentie à un salarié, retenue ultérieurement sur un bulletin.
/// </summary>
public sealed class EmployeeAdvance : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public DateTime Date { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reason { get; private set; }
    /// <summary>Vrai si l'avance a été intégralement retenue (soldée) sur un bulletin.</summary>
    public bool IsSettled { get; private set; }
    /// <summary>Cycle de paie sur lequel l'avance a été retenue.</summary>
    public Guid? SettledInPayrollRunId { get; private set; }
    /// <summary>
    /// R-22 : cumul des montants déjà retenus sur cette avance (règlement partiel possible quand
    /// le net disponible est insuffisant). 0 tant que rien n'a été retenu ; <see cref="Amount"/>
    /// une fois soldée. Figé pour traçabilité et calcul du reliquat à retenir.
    /// </summary>
    public decimal SettledAmount { get; private set; }

    /// <summary>Reliquat restant à retenir sur cette avance.</summary>
    public decimal RemainingAmount => R(Amount - SettledAmount);

    private EmployeeAdvance() { }

    public static Result<EmployeeAdvance> Create(Guid employeeId, DateTime date, decimal amount, string? reason = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeeAdvance>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (amount <= 0)
            return Result.Failure<EmployeeAdvance>(Error.Validation("Amount", "Le montant de l'avance doit être strictement positif."));

        return Result.Success(new EmployeeAdvance
        {
            EmployeeId = employeeId,
            Date = date.Date,
            Amount = Math.Round(amount, 3),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
    }

    /// <summary>Soldage intégral de l'avance sur le cycle indiqué.</summary>
    public void Settle(Guid payrollRunId)
    {
        SettledAmount = Amount;
        IsSettled = true;
        SettledInPayrollRunId = payrollRunId;
        IncrementVersion();
    }

    /// <summary>
    /// R-22 : retenue partielle de l'avance quand le net disponible est insuffisant pour absorber
    /// la totalité. Le reliquat (<see cref="RemainingAmount"/>) reste à retenir sur un cycle
    /// ultérieur ; l'avance n'est marquée soldée que lorsque le reliquat tombe à zéro. Le cycle
    /// indiqué est figé dès qu'au moins une retenue est appliquée.
    /// </summary>
    public Result SettlePartial(Guid payrollRunId, decimal appliedAmount)
    {
        if (appliedAmount <= 0)
            return Result.Failure(Error.Validation("AppliedAmount", "Le montant retenue doit être strictement positif."));
        if (IsSettled)
            return Result.Failure(Error.Validation("Advance", "L'avance est déjà soldée."));
        if (appliedAmount > RemainingAmount + 0.001m)
            return Result.Failure(Error.Validation("AppliedAmount",
                $"Le montant à retenir ({appliedAmount:N3}) dépasse le reliquat de l'avance ({RemainingAmount:N3})."));

        SettledAmount = R(SettledAmount + appliedAmount);
        if (RemainingAmount <= 0.001m)
        {
            SettledAmount = Amount;
            IsSettled = true;
        }
        SettledInPayrollRunId ??= payrollRunId;
        IncrementVersion();
        return Result.Success();
    }

    public void Unsettle()
    {
        SettledAmount = 0m;
        IsSettled = false;
        SettledInPayrollRunId = null;
        IncrementVersion();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
