using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.CnssIjClaims;

public sealed record ListCnssIjClaimsQuery(Guid? EmployeeId, int? Year, int? Month)
    : IRequest<IReadOnlyList<CnssIjClaimDto>>;

public sealed class ListCnssIjClaimsQueryHandler : IRequestHandler<ListCnssIjClaimsQuery, IReadOnlyList<CnssIjClaimDto>>
{
    private readonly ICnssIjClaimRepository _claims;
    private readonly IEmployeeRepository _employees;
    private readonly AccountingSettings _settings;

    public ListCnssIjClaimsQueryHandler(
        ICnssIjClaimRepository claims,
        IEmployeeRepository employees,
        IOptions<AccountingSettings> settings)
    {
        _claims = claims;
        _employees = employees;
        _settings = settings.Value;
    }

    public async Task<IReadOnlyList<CnssIjClaimDto>> Handle(ListCnssIjClaimsQuery request, CancellationToken cancellationToken)
    {
        if (!_settings.PayrollStatutorySickLeaveEnabled && !_settings.PayrollStatutoryMaternityLeaveEnabled)
            return Array.Empty<CnssIjClaimDto>();

        IReadOnlyList<CnssIjClaim> items;
        if (request.EmployeeId.HasValue)
        {
            items = await _claims.ListByEmployeeAsync(request.EmployeeId.Value, cancellationToken);
        }
        else if (request.Year.HasValue)
        {
            items = await _claims.ListForPeriodAsync(request.Year.Value, request.Month, cancellationToken);
        }
        else
        {
            items = await _claims.GetAllAsync(cancellationToken);
        }

        var employeeIds = items.Select(c => c.EmployeeId).Distinct().ToList();
        var names = new Dictionary<Guid, string>();
        foreach (var id in employeeIds)
        {
            var employee = await _employees.GetByIdAsync(id, cancellationToken);
            if (employee is not null)
                names[id] = employee.FullName;
        }

        return items.Select(c => PayrollMappings.ToDto(c, names.GetValueOrDefault(c.EmployeeId))).ToList();
    }
}

public sealed record MarkCnssIjClaimPaidCommand(Guid Id, DateTime PaidAt) : IRequest<Result>;

public sealed class MarkCnssIjClaimPaidCommandValidator : AbstractValidator<MarkCnssIjClaimPaidCommand>
{
    public MarkCnssIjClaimPaidCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PaidAt).NotEmpty();
    }
}

public sealed class MarkCnssIjClaimPaidCommandHandler : IRequestHandler<MarkCnssIjClaimPaidCommand, Result>
{
    private readonly ICnssIjClaimRepository _claims;
    private readonly AccountingSettings _settings;

    public MarkCnssIjClaimPaidCommandHandler(
        ICnssIjClaimRepository claims,
        IOptions<AccountingSettings> settings)
    {
        _claims = claims;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(MarkCnssIjClaimPaidCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.PayrollStatutorySickLeaveEnabled && !_settings.PayrollStatutoryMaternityLeaveEnabled)
        {
            return Result.Failure(Error.Validation(
                "Feature",
                "Le suivi des créances IJ CNSS n'est pas activé."));
        }

        var claim = await _claims.GetByIdAsync(request.Id, cancellationToken);
        if (claim is null)
            return Result.Failure(Error.NotFound("CnssIjClaim", request.Id));

        var result = claim.MarkPaid(request.PaidAt);
        if (result.IsFailure)
            return result;

        await _claims.UpdateAsync(claim, cancellationToken);
        return Result.Success();
    }
}
