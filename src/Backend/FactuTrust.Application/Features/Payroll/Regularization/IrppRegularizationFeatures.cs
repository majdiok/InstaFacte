using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Regularization;

/// <summary>
/// Régularisation IRPP/CSS annuelle : génération batch sur un cycle calculé, prévisualisation
/// à la demande, ajustement manuel et consultation.
///
/// La régularisation est une variable du mois, comme les heures supplémentaires : elle est
/// verrouillée dès que le cycle du mois est validé ou clôturé.
/// </summary>
internal static class IrppRegularizationAssembler
{
    /// <summary>
    /// Assemble le cumul mensuel : mois antérieurs arrêtés (Validés/Clôturés) puis le mois
    /// régularisé lui-même. Les montants lus sont l'IRPP et la CSS <b>mensuels purs</b> —
    /// jamais les colonnes de régularisation — ce qui rend la régénération idempotente.
    /// </summary>
    public static List<IrppRegularizationMonth> BuildMonths(
        IEnumerable<Payslip> settledPayslips,
        Payslip? currentMonthPayslip)
    {
        var months = settledPayslips
            .OrderBy(p => p.Month)
            .Select(p => new IrppRegularizationMonth(
                p.Month,
                p.MonthlyNetTaxable,
                p.Irpp,
                p.Css,
                ResolveBaseSalary(p)))
            .ToList();

        if (currentMonthPayslip is not null)
        {
            months.RemoveAll(m => m.Month == currentMonthPayslip.Month);
            months.Add(new IrppRegularizationMonth(
                currentMonthPayslip.Month,
                currentMonthPayslip.MonthlyNetTaxable,
                currentMonthPayslip.Irpp,
                currentMonthPayslip.Css,
                ResolveBaseSalary(currentMonthPayslip)));
        }

        return months.OrderBy(m => m.Month).ToList();
    }

    public static List<IrppRegularizationMonthDto> ToMonthDtos(
        IReadOnlyList<IrppRegularizationMonth> months,
        int currentMonth)
    {
        return months
            .Select(m => new IrppRegularizationMonthDto
            {
                Month = m.Month,
                MonthLabel = PayrollMappings.ToMonthLabel(m.Month),
                MonthlyNetTaxable = m.MonthlyNetTaxable,
                Irpp = m.Irpp,
                Css = m.Css,
                IsSettled = m.Month != currentMonth
            })
            .ToList();
    }

    private static decimal ResolveBaseSalary(Payslip payslip) =>
        payslip.Lines.FirstOrDefault(l => l.Label == "Salaire de base")?.Amount ?? payslip.GrossSalary;

    public static string SerializeDetail(IReadOnlyList<IrppRegularizationMonth> months) =>
        JsonSerializer.Serialize(months);

    /// <summary>
    /// Motif applicable : décembre pour la régularisation de fin d'exercice, solde de tout
    /// compte lorsque le contrat s'achève dans le mois du cycle.
    /// </summary>
    public static IrppRegularizationReason? ResolveReason(Employee employee, int year, int month)
    {
        var referenceDate = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        var contract = employee.GetActiveContract(referenceDate);

        if (contract?.EndDate is { } endDate && endDate.Year == year && endDate.Month == month)
            return IrppRegularizationReason.FinalSettlement;

        if (month == 12)
            return IrppRegularizationReason.YearEnd;

        return null;
    }
}

// ---------------------------------------------------------------------------------------
// Génération batch
// ---------------------------------------------------------------------------------------

public sealed record GenerateIrppRegularizationsCommand(Guid RunId)
    : IRequest<Result<GenerateIrppRegularizationsResultDto>>;

public sealed class GenerateIrppRegularizationsCommandHandler
    : IRequestHandler<GenerateIrppRegularizationsCommand, Result<GenerateIrppRegularizationsResultDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;
    private readonly IPayrollIrppRegularizationRepository _regularizations;
    private readonly ITenantUnitOfWork _unitOfWork;

    public GenerateIrppRegularizationsCommandHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        IPayrollParametersRepository parameters,
        IPayrollIrppRegularizationRepository regularizations,
        ITenantUnitOfWork unitOfWork)
    {
        _runs = runs;
        _employees = employees;
        _parameters = parameters;
        _regularizations = regularizations;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GenerateIrppRegularizationsResultDto>> Handle(
        GenerateIrppRegularizationsCommand request,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            var run = await _runs.GetByIdWithPayslipsAsync(request.RunId, ct);
            if (run is null)
                return Result.Failure<GenerateIrppRegularizationsResultDto>(Error.NotFound("PayrollRun", request.RunId));

            // Le cycle doit être calculé : le bulletin du mois régularisé entre dans le cumul.
            if (run.Status != PayrollRunStatus.Calculated)
            {
                return Result.Failure<GenerateIrppRegularizationsResultDto>(Error.Validation(
                    "Status",
                    "Le cycle doit être calculé (et non encore validé) pour générer les régularisations."));
            }

            var parameters = await _parameters.GetOrCreateForYearAsync(run.ParametersFiscalYear, ct);
            if (!parameters.EnableIrppRegularization)
            {
                return Result.Failure<GenerateIrppRegularizationsResultDto>(Error.Validation(
                    "EnableIrppRegularization",
                    "La régularisation IRPP n'est pas activée pour cet exercice (RH & Paie → Paramètres paie)."));
            }

            var employees = await _employees.GetActiveWithContractsAsync(ct);
            var employeesById = employees.ToDictionary(e => e.Id);

            var settled = await _runs.ListSettledPayslipsForYearAsync(run.Year, run.Month, ct);
            var settledByEmployee = settled
                .GroupBy(p => p.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var existing = await _regularizations.ListForMonthAsync(run.Year, run.Month, ct);
            var existingByEmployee = existing.ToDictionary(r => r.EmployeeId);

            var toAdd = new List<PayrollIrppRegularization>();
            var toUpdate = new List<PayrollIrppRegularization>();
            var skipped = 0;
            var preservedOverrides = 0;
            decimal totalDelta = 0m;

            foreach (var payslip in run.Payslips)
            {
                if (!employeesById.TryGetValue(payslip.EmployeeId, out var employee))
                {
                    skipped++;
                    continue;
                }

                var reason = IrppRegularizationAssembler.ResolveReason(employee, run.Year, run.Month);
                if (reason is null)
                {
                    skipped++;
                    continue;
                }

                var months = IrppRegularizationAssembler.BuildMonths(
                    settledByEmployee.GetValueOrDefault(payslip.EmployeeId, []),
                    payslip);

                var result = IrppRegularizationCalculator.Compute(months, parameters);
                var detailJson = IrppRegularizationAssembler.SerializeDetail(months);

                if (existingByEmployee.TryGetValue(payslip.EmployeeId, out var line))
                {
                    // Un ajustement manuel n'est jamais écrasé en silence : les cumuls sont
                    // rafraîchis, le montant forcé reste et l'écran signale la ligne.
                    if (line.IsOverridden)
                        preservedOverrides++;

                    line.Refresh(result, detailJson);
                    line.SetReason(reason.Value);
                    toUpdate.Add(line);
                    totalDelta += line.EffectiveTotalDelta;
                    continue;
                }

                // Rien à porter : on ne crée pas de ligne à zéro pour ne pas encombrer l'écran.
                if (result.IsNeutral)
                {
                    skipped++;
                    continue;
                }

                var created = PayrollIrppRegularization.Create(
                    payslip.EmployeeId, run.Year, run.Month, reason.Value, result, detailJson);

                if (created.IsFailure)
                    return Result.Failure<GenerateIrppRegularizationsResultDto>(created.Error);

                toAdd.Add(created.Value);
                totalDelta += created.Value.EffectiveTotalDelta;
            }

            await _regularizations.SaveBatchAsync(toAdd, toUpdate, ct);

            return Result.Success(new GenerateIrppRegularizationsResultDto
            {
                Created = toAdd.Count,
                Updated = toUpdate.Count,
                Skipped = skipped,
                PreservedOverrides = preservedOverrides,
                TotalDelta = Math.Round(totalDelta, 3, MidpointRounding.AwayFromZero)
            });
        }, cancellationToken);
    }
}

// ---------------------------------------------------------------------------------------
// Prévisualisation (bouton « Calculer » de l'écran)
// ---------------------------------------------------------------------------------------

public sealed record PreviewIrppRegularizationQuery(Guid EmployeeId, int Year, int Month)
    : IRequest<Result<IrppRegularizationPreviewDto>>;

public sealed class PreviewIrppRegularizationQueryValidator : AbstractValidator<PreviewIrppRegularizationQuery>
{
    public PreviewIrppRegularizationQueryValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class PreviewIrppRegularizationQueryHandler
    : IRequestHandler<PreviewIrppRegularizationQuery, Result<IrppRegularizationPreviewDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;

    public PreviewIrppRegularizationQueryHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        IPayrollParametersRepository parameters)
    {
        _runs = runs;
        _employees = employees;
        _parameters = parameters;
    }

    public async Task<Result<IrppRegularizationPreviewDto>> Handle(
        PreviewIrppRegularizationQuery request,
        CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdWithContractsAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<IrppRegularizationPreviewDto>(Error.NotFound("Employee", request.EmployeeId));

        var parameters = await _parameters.GetOrCreateForYearAsync(request.Year, cancellationToken);

        var settled = await _runs.ListSettledPayslipsForYearAsync(request.Year, request.Month, cancellationToken);
        var employeeSettled = settled.Where(p => p.EmployeeId == request.EmployeeId).ToList();

        // Bulletin du mois régularisé, s'il a déjà été calculé.
        var currentRun = await _runs.GetByPeriodAsync(request.Year, request.Month, cancellationToken);
        Payslip? currentPayslip = null;
        if (currentRun is not null)
        {
            var withPayslips = await _runs.GetByIdWithPayslipsAsync(currentRun.Id, cancellationToken);
            currentPayslip = withPayslips?.Payslips.FirstOrDefault(p => p.EmployeeId == request.EmployeeId);
        }

        var months = IrppRegularizationAssembler.BuildMonths(employeeSettled, currentPayslip);
        var result = IrppRegularizationCalculator.Compute(months, parameters);
        var reason = IrppRegularizationAssembler.ResolveReason(employee, request.Year, request.Month)
                     ?? IrppRegularizationReason.Manual;

        return Result.Success(new IrppRegularizationPreviewDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            EmployeeNumber = employee.EmployeeNumber,
            Year = request.Year,
            Month = request.Month,
            Reason = (int)reason,
            ReasonLabel = reason.ToDisplayString(),
            MonthsCounted = result.MonthsCounted,
            CumulNetTaxable = result.CumulNetTaxable,
            CumulIrppWithheld = result.CumulIrppWithheld,
            CumulCssWithheld = result.CumulCssWithheld,
            IrppDue = result.IrppDue,
            CssDue = result.CssDue,
            IrppDelta = result.IrppDelta,
            CssDelta = result.CssDelta,
            TotalDelta = result.TotalDelta,
            IsAdditionalWithholding = result.IsAdditionalWithholding,
            IsFeatureDisabled = !parameters.EnableIrppRegularization,
            IsPartialYear = result.MonthsCounted < request.Month,
            Months = IrppRegularizationAssembler.ToMonthDtos(months, request.Month)
        });
    }
}

// ---------------------------------------------------------------------------------------
// Consultation
// ---------------------------------------------------------------------------------------

public sealed record ListIrppRegularizationsForMonthQuery(int Year, int Month)
    : IRequest<IReadOnlyList<IrppRegularizationDto>>;

public sealed class ListIrppRegularizationsForMonthQueryHandler
    : IRequestHandler<ListIrppRegularizationsForMonthQuery, IReadOnlyList<IrppRegularizationDto>>
{
    private readonly IPayrollIrppRegularizationRepository _regularizations;
    private readonly IEmployeeRepository _employees;

    public ListIrppRegularizationsForMonthQueryHandler(
        IPayrollIrppRegularizationRepository regularizations,
        IEmployeeRepository employees)
    {
        _regularizations = regularizations;
        _employees = employees;
    }

    public async Task<IReadOnlyList<IrppRegularizationDto>> Handle(
        ListIrppRegularizationsForMonthQuery request,
        CancellationToken cancellationToken)
    {
        var lines = await _regularizations.ListForMonthAsync(request.Year, request.Month, cancellationToken);
        if (lines.Count == 0)
            return Array.Empty<IrppRegularizationDto>();

        var names = await _employees.GetFullNamesByIdsAsync(
            lines.Select(l => l.EmployeeId).Distinct().ToList(), cancellationToken);

        return lines
            .Select(l => PayrollMappings.ToIrppRegularizationDto(
                l,
                names.GetValueOrDefault(l.EmployeeId),
                months: DeserializeMonths(l)))
            .ToList();
    }

    private static IReadOnlyList<IrppRegularizationMonthDto> DeserializeMonths(PayrollIrppRegularization line)
    {
        if (string.IsNullOrWhiteSpace(line.DetailJson))
            return Array.Empty<IrppRegularizationMonthDto>();

        try
        {
            var months = JsonSerializer.Deserialize<List<IrppRegularizationMonth>>(line.DetailJson);
            return months is null
                ? Array.Empty<IrppRegularizationMonthDto>()
                : IrppRegularizationAssembler.ToMonthDtos(months, line.Month);
        }
        catch (JsonException)
        {
            // Instantané illisible (format ancien) : le détail est purement informatif,
            // les montants font foi et restent exploitables.
            return Array.Empty<IrppRegularizationMonthDto>();
        }
    }
}

// ---------------------------------------------------------------------------------------
// Ajustement manuel
// ---------------------------------------------------------------------------------------

public sealed record UpsertIrppRegularizationCommand(UpsertIrppRegularizationDto Dto) : IRequest<Result<Guid>>;

public sealed class UpsertIrppRegularizationCommandValidator : AbstractValidator<UpsertIrppRegularizationCommand>
{
    public UpsertIrppRegularizationCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Dto.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Dto.Notes).MaximumLength(1000);
    }
}

public sealed class UpsertIrppRegularizationCommandHandler
    : IRequestHandler<UpsertIrppRegularizationCommand, Result<Guid>>
{
    private readonly IPayrollIrppRegularizationRepository _regularizations;
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollParametersRepository _parameters;

    public UpsertIrppRegularizationCommandHandler(
        IPayrollIrppRegularizationRepository regularizations,
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        IPayrollParametersRepository parameters)
    {
        _regularizations = regularizations;
        _runs = runs;
        _employees = employees;
        _parameters = parameters;
    }

    public async Task<Result<Guid>> Handle(UpsertIrppRegularizationCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(dto.Year, dto.Month, cancellationToken))
            return Result.Failure<Guid>(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        var employee = await _employees.GetByIdWithContractsAsync(dto.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var existing = await _regularizations.GetByEmployeeAndMonthAsync(
            dto.EmployeeId, dto.Year, dto.Month, cancellationToken);

        if (existing is not null)
        {
            existing.SetOverride(dto.OverrideIrppDelta, dto.OverrideCssDelta, dto.Notes);
            await _regularizations.UpdateAsync(existing, cancellationToken);
            return Result.Success(existing.Id);
        }

        // Création à la volée : on calcule les cumuls pour que la ligne reste justifiable,
        // puis on applique l'ajustement demandé.
        var parameters = await _parameters.GetOrCreateForYearAsync(dto.Year, cancellationToken);
        var settled = await _runs.ListSettledPayslipsForYearAsync(dto.Year, dto.Month, cancellationToken);
        var employeeSettled = settled.Where(p => p.EmployeeId == dto.EmployeeId).ToList();

        Payslip? currentPayslip = null;
        var currentRun = await _runs.GetByPeriodAsync(dto.Year, dto.Month, cancellationToken);
        if (currentRun is not null)
        {
            var withPayslips = await _runs.GetByIdWithPayslipsAsync(currentRun.Id, cancellationToken);
            currentPayslip = withPayslips?.Payslips.FirstOrDefault(p => p.EmployeeId == dto.EmployeeId);
        }

        var months = IrppRegularizationAssembler.BuildMonths(employeeSettled, currentPayslip);
        var result = IrppRegularizationCalculator.Compute(months, parameters);
        var reason = IrppRegularizationAssembler.ResolveReason(employee, dto.Year, dto.Month)
                     ?? IrppRegularizationReason.Manual;

        var created = PayrollIrppRegularization.Create(
            dto.EmployeeId,
            dto.Year,
            dto.Month,
            reason,
            result,
            IrppRegularizationAssembler.SerializeDetail(months),
            dto.Notes);

        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error);

        if (dto.OverrideIrppDelta.HasValue || dto.OverrideCssDelta.HasValue)
            created.Value.SetOverride(dto.OverrideIrppDelta, dto.OverrideCssDelta, dto.Notes);

        await _regularizations.AddAsync(created.Value, cancellationToken);
        return Result.Success(created.Value.Id);
    }
}

// ---------------------------------------------------------------------------------------
// Suppression
// ---------------------------------------------------------------------------------------

public sealed record DeleteIrppRegularizationCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteIrppRegularizationCommandHandler : IRequestHandler<DeleteIrppRegularizationCommand, Result>
{
    private readonly IPayrollIrppRegularizationRepository _regularizations;
    private readonly IPayrollRunRepository _runs;

    public DeleteIrppRegularizationCommandHandler(
        IPayrollIrppRegularizationRepository regularizations,
        IPayrollRunRepository runs)
    {
        _regularizations = regularizations;
        _runs = runs;
    }

    public async Task<Result> Handle(DeleteIrppRegularizationCommand request, CancellationToken cancellationToken)
    {
        var line = await _regularizations.GetByIdAsync(request.Id, cancellationToken);
        if (line is null)
            return Result.Failure(Error.NotFound("PayrollIrppRegularization", request.Id));

        if (await _runs.HasValidatedOrClosedRunForMonthAsync(line.Year, line.Month, cancellationToken))
            return Result.Failure(Error.Validation("Month", "Le mois de paie est verrouillé (cycle validé ou clôturé)."));

        await _regularizations.DeleteAsync(line, cancellationToken);
        return Result.Success();
    }
}
