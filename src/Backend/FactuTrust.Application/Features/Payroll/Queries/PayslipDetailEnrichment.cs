using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.LeaveBalance;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Application.Features.Payroll.Queries;

/// <summary>
/// Enrichit un <see cref="PayslipDetailDto"/> avec les données d'affichage
/// (identité, fonction, jours travaillés, solde congés) non figées sur le bulletin.
/// </summary>
internal static class PayslipDetailEnrichment
{
    public static async Task<PayslipDetailDto> EnrichAsync(
        PayslipDetailDto dto,
        Employee? employee,
        int year,
        int month,
        ILeaveBalanceAccrualRepository accruals,
        ILeaveRequestRepository leaves,
        string? companyAddress,
        CancellationToken cancellationToken)
    {
        if (employee is null)
        {
            return dto with { CompanyAddress = companyAddress };
        }

        var balance = await GetEmployeeLeaveBalanceQueryHandler.BuildBalanceDtoAsync(
            employee, year, accruals, leaves, cancellationToken);

        var monthLeaves = await leaves.ListForMonthAsync(year, month, cancellationToken);
        var unpaidDays = monthLeaves
            .Where(l => l.EmployeeId == employee.Id && l.IsApproved && l.Type.ReducesGross())
            .Sum(l => l.Days);
        var workedDays = LeaveBalanceService.ComputeWorkedDays(unpaidDays);

        var periodDate = new DateTime(year, month, 1);
        var contract = employee.GetActiveContract(periodDate)
            ?? employee.Contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();

        return dto with
        {
            Cin = employee.Cin,
            HireDate = employee.HireDate,
            LeaveBalanceRemaining = balance.Remaining,
            JobTitle = contract?.JobTitle,
            Category = employee.Category,
            Echelon = employee.Echelon,
            IsHeadOfFamily = employee.IsHeadOfFamily,
            WorkedDays = workedDays,
            CompanyAddress = companyAddress
        };
    }
}
