using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Ventilation d'un paiement de paie sur un bulletin (un salarié).
/// </summary>
public sealed class PayrollPaymentLine : Entity
{
    public Guid PayrollPaymentId { get; private set; }
    public Guid PayslipId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Money Amount { get; private set; } = null!;
    /// <summary>Snapshot du compte auxiliaire 421xxxx utilisé pour la comptabilité.</summary>
    public string EmployeeAuxiliaryAccount { get; private set; } = null!;

    private PayrollPaymentLine() { }

    internal static Result<PayrollPaymentLine> Create(
        Payslip payslip,
        decimal amount,
        string employeeAuxiliaryAccount,
        string currency)
    {
        if (amount <= 0)
            return Result.Failure<PayrollPaymentLine>(Error.Validation("Amount", "Le montant doit être positif."));

        var remaining = payslip.RemainingToPay;
        if (amount > remaining + 0.001m)
        {
            return Result.Failure<PayrollPaymentLine>(Error.Validation(
                "Amount",
                $"Le montant ({amount:N3}) dépasse le reste à payer ({remaining:N3}) pour {payslip.EmployeeName}."));
        }

        if (string.IsNullOrWhiteSpace(employeeAuxiliaryAccount))
        {
            return Result.Failure<PayrollPaymentLine>(Error.Validation(
                "EmployeeAuxiliaryAccount",
                "Le compte auxiliaire salarié est obligatoire."));
        }

        return Result.Success(new PayrollPaymentLine
        {
            PayslipId = payslip.Id,
            EmployeeId = payslip.EmployeeId,
            Amount = Money.Create(amount, currency),
            EmployeeAuxiliaryAccount = employeeAuxiliaryAccount.Trim()
        });
    }
}
