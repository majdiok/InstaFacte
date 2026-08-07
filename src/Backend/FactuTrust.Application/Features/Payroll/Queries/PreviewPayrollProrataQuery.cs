using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Services.Payroll;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record PreviewPayrollProrataQuery(Guid RunId) : IRequest<Result<PayrollProrataPreviewDto>>;

public sealed class PreviewPayrollProrataQueryHandler
    : IRequestHandler<PreviewPayrollProrataQuery, Result<PayrollProrataPreviewDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;
    private readonly IEmployeePayrollSuspensionRepository _suspensions;
    private readonly IPayrollPublicHolidayRepository _publicHolidays;
    private readonly AccountingSettings _settings;

    public PreviewPayrollProrataQueryHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        IPayrollParametersRepository parameters,
        IEmployeePayrollSuspensionRepository suspensions,
        IPayrollPublicHolidayRepository publicHolidays,
        IOptions<AccountingSettings> settings)
    {
        _runs = runs;
        _employees = employees;
        _parameters = parameters;
        _suspensions = suspensions;
        _publicHolidays = publicHolidays;
        _settings = settings.Value;
    }

    public async Task<Result<PayrollProrataPreviewDto>> Handle(
        PreviewPayrollProrataQuery request,
        CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure<PayrollProrataPreviewDto>(Error.NotFound("PayrollRun", request.RunId));

        var parameters = await _parameters.GetOrCreateForYearAsync(run.ParametersFiscalYear, cancellationToken);
        if (!parameters.EnableAutomaticProrata)
        {
            return Result.Success(new PayrollProrataPreviewDto
            {
                Year = run.Year,
                Month = run.Month,
                IsEnabled = false
            });
        }

        var employees = await _employees.GetEligibleForPayrollMonthAsync(run.Year, run.Month, cancellationToken);
        var monthSuspensions = await _suspensions.ListForMonthAsync(run.Year, run.Month, cancellationToken);
        var publicHolidays = _settings.PayrollPublicHolidaysEnabled
            ? await _publicHolidays.ListForMonthAsync(run.Year, run.Month, cancellationToken)
            : Array.Empty<PayrollPublicHoliday>();
        var nonPaidHolidayDates = publicHolidays.Where(h => !h.IsPaid).Select(h => h.Date.Date).ToHashSet();
        var holidayDates = nonPaidHolidayDates.Count > 0 ? nonPaidHolidayDates : null;
        var monthStart = new DateTime(run.Year, run.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var lines = new List<PayrollProrataPreviewLineDto>();

        foreach (var employee in employees)
        {
            var contract = employee.GetContractForPayrollMonth(run.Year, run.Month);
            if (contract is null)
                continue;

            var effectiveStart = MaxDate(monthStart, contract.StartDate, employee.HireDate);
            var effectiveEnd = MinDate(monthEnd, contract.EndDate ?? monthEnd, employee.TerminationDate ?? monthEnd);

            var suspensionPeriods = monthSuspensions
                .Where(s => s.EmployeeId == employee.Id)
                .Select(s => new PayrollProrataSuspensionPeriod(s.StartDate, s.EndDate, s.IsPaid, s.IsApproved))
                .ToList();

            var prorata = PayrollProrataCalculator.Compute(new PayrollProrataMonthInput
            {
                Year = run.Year,
                Month = run.Month,
                BaseSalary = contract.BaseSalary,
                EffectiveStart = effectiveStart,
                EffectiveEnd = effectiveEnd,
                IsEnabled = true,
                Suspensions = suspensionPeriods,
                NonPaidHolidayDates = holidayDates
            });

            if (prorata.DeductionAmount <= 0 && !employee.IsActive && !employee.TerminationDate.HasValue)
            {
                lines.Add(new PayrollProrataPreviewLineDto
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.FullName,
                    EmployeeNumber = employee.EmployeeNumber,
                    WorkedDays = prorata.WorkedDays,
                    NonWorkedDays = prorata.NonWorkedDays,
                    DeductionAmount = prorata.DeductionAmount,
                    Reason = prorata.Reason.ToString(),
                    Warnings = ["Salarié inactif sans date de sortie — vérifiez la fiche salarié."]
                });
                continue;
            }

            if (prorata.DeductionAmount <= 0)
                continue;

            lines.Add(new PayrollProrataPreviewLineDto
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.FullName,
                EmployeeNumber = employee.EmployeeNumber,
                WorkedDays = prorata.WorkedDays,
                NonWorkedDays = prorata.NonWorkedDays,
                DeductionAmount = prorata.DeductionAmount,
                Reason = prorata.Reason.ToString(),
                Warnings = prorata.Warnings.ToList()
            });
        }

        return Result.Success(new PayrollProrataPreviewDto
        {
            Year = run.Year,
            Month = run.Month,
            IsEnabled = true,
            EmployeeCount = lines.Count,
            TotalDeduction = lines.Sum(l => l.DeductionAmount),
            Lines = lines
        });
    }

    private static DateTime MaxDate(DateTime a, DateTime b, DateTime c) =>
        new[] { a.Date, b.Date, c.Date }.Max();

    private static DateTime MinDate(DateTime a, DateTime b, DateTime c) =>
        new[] { a.Date, b.Date, c.Date }.Min();
}
