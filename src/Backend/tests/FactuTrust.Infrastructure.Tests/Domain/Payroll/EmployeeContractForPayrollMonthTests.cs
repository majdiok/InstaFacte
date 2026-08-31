using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// R-29 : un salarié parti en cours de mois doit recevoir son bulletin final (plein mois, hors
/// chemin prorata). L'ancienne résolution <c>GetActiveContract(date de fin de mois)</c> retournait
/// null pour un contrat finissant avant la fin du mois → aucun bulletin. <c>GetContractForPayrollMonth</c>
/// retourne le contrat couvrant au moins un jour du mois, inclusant donc le départ mi-mois.
/// </summary>
public sealed class EmployeeContractForPayrollMonthTests
{
    private static Employee NewEmployee(string number = "E001") =>
        Employee.Create(number, "Jean", "Dupont", new DateTime(2025, 1, 1)).Value;

    [Fact]
    public void GetContractForPayrollMonth_IncludesTerminatedMidMonthEmployee_ForFinalPayslip()
    {
        var emp = NewEmployee();
        Assert.True(emp.AddContract(
            ContractType.Cdi, SocialRegime.Rsna, new DateTime(2025, 1, 1), 2000m, 0.4m,
            endDate: new DateTime(2026, 1, 15)).IsSuccess);
        emp.Terminate(new DateTime(2026, 1, 15));
        Assert.False(emp.IsActive);

        // Ancien comportement : le contrat (fin 15/01) ne couvre pas le 31/01 → null → pas de bulletin.
        Assert.Null(emp.GetActiveContract(new DateTime(2026, 1, 31)));

        // R-29 (fix) : le contrat couvre au moins un jour du mois → bulletin final produit.
        var finalContract = emp.GetContractForPayrollMonth(2026, 1);
        Assert.NotNull(finalContract);
        Assert.Equal(new DateTime(2026, 1, 15), finalContract!.EndDate);
    }

    [Fact]
    public void GetContractForPayrollMonth_ExcludesContractEndedBeforeMonth()
    {
        // Contrat terminé fin du mois précédent → aucun jour dans le mois de paie → exclu
        // (la correction R-29 n'est pas sur-inclusive).
        var emp = NewEmployee("E002");
        Assert.True(emp.AddContract(
            ContractType.Cdi, SocialRegime.Rsna, new DateTime(2024, 1, 1), 2000m, 0.4m,
            endDate: new DateTime(2025, 12, 31)).IsSuccess);
        emp.Terminate(new DateTime(2025, 12, 31));

        Assert.Null(emp.GetContractForPayrollMonth(2026, 1));
    }

    [Fact]
    public void GetContractForPayrollMonth_IncludesActiveEmployee_WithOpenEndedContract()
    {
        var emp = NewEmployee("E003");
        Assert.True(emp.AddContract(
            ContractType.Cdi, SocialRegime.Rsna, new DateTime(2025, 1, 1), 2000m, 0.4m,
            endDate: null).IsSuccess);

        var contract = emp.GetContractForPayrollMonth(2026, 1);
        Assert.NotNull(contract);
        Assert.Null(contract!.EndDate);
    }
}
