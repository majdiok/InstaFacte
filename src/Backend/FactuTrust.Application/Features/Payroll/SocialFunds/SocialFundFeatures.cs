using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.SocialFunds;

public sealed record ListSocialFundSchemesQuery(bool ActiveOnly = false) : IRequest<IReadOnlyList<SocialFundSchemeDto>>;

public sealed class ListSocialFundSchemesQueryHandler : IRequestHandler<ListSocialFundSchemesQuery, IReadOnlyList<SocialFundSchemeDto>>
{
    private readonly ISocialFundSchemeRepository _schemes;

    public ListSocialFundSchemesQueryHandler(ISocialFundSchemeRepository schemes) => _schemes = schemes;

    public async Task<IReadOnlyList<SocialFundSchemeDto>> Handle(ListSocialFundSchemesQuery request, CancellationToken cancellationToken)
    {
        var schemes = await _schemes.ListAsync(request.ActiveOnly, cancellationToken);
        return schemes.Select(PayrollMappings.ToSocialFundSchemeDto).ToList();
    }
}

public sealed record CreateSocialFundSchemeCommand(UpsertSocialFundSchemeDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateSocialFundSchemeCommandHandler : IRequestHandler<CreateSocialFundSchemeCommand, Result<Guid>>
{
    private readonly ISocialFundSchemeRepository _schemes;

    public CreateSocialFundSchemeCommandHandler(ISocialFundSchemeRepository schemes) => _schemes = schemes;

    public async Task<Result<Guid>> Handle(CreateSocialFundSchemeCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (await _schemes.GetByCodeAsync(dto.Code, cancellationToken) is not null)
            return Result.Failure<Guid>(Error.Validation("Code", "Ce code de caisse existe déjà."));

        var result = SocialFundScheme.Create(
            dto.Code, dto.Name, dto.EmployeeRatePercent, dto.EmployerRatePercent, dto.Base,
            dto.FixedEmployeeAmount, dto.FixedEmployerAmount, dto.MonthlyEmployeeCap,
            dto.EmployeeAccountSce, dto.EmployerAccountSce, dto.EffectiveFrom, dto.EffectiveTo);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await _schemes.AddAsync(result.Value, cancellationToken);
        return Result.Success(result.Value.Id);
    }
}

public sealed record UpdateSocialFundSchemeCommand(Guid Id, UpsertSocialFundSchemeDto Dto) : IRequest<Result>;

public sealed class UpdateSocialFundSchemeCommandHandler : IRequestHandler<UpdateSocialFundSchemeCommand, Result>
{
    private readonly ISocialFundSchemeRepository _schemes;

    public UpdateSocialFundSchemeCommandHandler(ISocialFundSchemeRepository schemes) => _schemes = schemes;

    public async Task<Result> Handle(UpdateSocialFundSchemeCommand request, CancellationToken cancellationToken)
    {
        var scheme = await _schemes.GetByIdAsync(request.Id, cancellationToken);
        if (scheme is null)
            return Result.Failure(Error.NotFound("SocialFundScheme", request.Id));

        var dto = request.Dto;
        var update = scheme.Update(
            dto.Name, dto.IsActive, dto.EmployeeRatePercent, dto.EmployerRatePercent, dto.Base,
            dto.FixedEmployeeAmount, dto.FixedEmployerAmount, dto.MonthlyEmployeeCap,
            dto.EmployeeAccountSce, dto.EmployerAccountSce, dto.EffectiveFrom, dto.EffectiveTo);

        if (update.IsFailure)
            return update;

        await _schemes.UpdateAsync(scheme, cancellationToken);
        return Result.Success();
    }
}

public sealed record ListEmployeeSocialFundEnrollmentsQuery(Guid EmployeeId) : IRequest<IReadOnlyList<EmployeeSocialFundEnrollmentDto>>;

public sealed class ListEmployeeSocialFundEnrollmentsQueryHandler : IRequestHandler<ListEmployeeSocialFundEnrollmentsQuery, IReadOnlyList<EmployeeSocialFundEnrollmentDto>>
{
    private readonly IEmployeeSocialFundEnrollmentRepository _enrollments;
    private readonly ISocialFundSchemeRepository _schemes;
    private readonly IEmployeeRepository _employees;

    public ListEmployeeSocialFundEnrollmentsQueryHandler(
        IEmployeeSocialFundEnrollmentRepository enrollments,
        ISocialFundSchemeRepository schemes,
        IEmployeeRepository employees)
    {
        _enrollments = enrollments;
        _schemes = schemes;
        _employees = employees;
    }

    public async Task<IReadOnlyList<EmployeeSocialFundEnrollmentDto>> Handle(
        ListEmployeeSocialFundEnrollmentsQuery request,
        CancellationToken cancellationToken)
    {
        var enrollments = await _enrollments.ListByEmployeeAsync(request.EmployeeId, cancellationToken);
        var schemes = (await _schemes.ListAsync(cancellationToken: cancellationToken)).ToDictionary(s => s.Id);
        var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        var employeeName = employee?.FullName;

        return enrollments
            .Select(e => PayrollMappings.ToSocialFundEnrollmentDto(
                e, employeeName, schemes.GetValueOrDefault(e.SocialFundSchemeId)?.Name))
            .ToList();
    }
}

public sealed record CreateEmployeeSocialFundEnrollmentCommand(UpsertEmployeeSocialFundEnrollmentDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateEmployeeSocialFundEnrollmentCommandHandler : IRequestHandler<CreateEmployeeSocialFundEnrollmentCommand, Result<Guid>>
{
    private readonly IEmployeeSocialFundEnrollmentRepository _enrollments;
    private readonly IEmployeeRepository _employees;
    private readonly ISocialFundSchemeRepository _schemes;

    public CreateEmployeeSocialFundEnrollmentCommandHandler(
        IEmployeeSocialFundEnrollmentRepository enrollments,
        IEmployeeRepository employees,
        ISocialFundSchemeRepository schemes)
    {
        _enrollments = enrollments;
        _employees = employees;
        _schemes = schemes;
    }

    public async Task<Result<Guid>> Handle(CreateEmployeeSocialFundEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (await _employees.GetByIdAsync(dto.EmployeeId, cancellationToken) is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));
        if (await _schemes.GetByIdAsync(dto.SocialFundSchemeId, cancellationToken) is null)
            return Result.Failure<Guid>(Error.NotFound("SocialFundScheme", dto.SocialFundSchemeId));

        var result = EmployeeSocialFundEnrollment.Create(
            dto.EmployeeId, dto.SocialFundSchemeId, dto.StartDate, dto.EndDate,
            dto.OverrideEmployeeAmount, dto.OverrideEmployerAmount);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await _enrollments.AddAsync(result.Value, cancellationToken);
        return Result.Success(result.Value.Id);
    }
}

public sealed record UpdateEmployeeSocialFundEnrollmentCommand(Guid Id, DateTime? EndDate, decimal? OverrideEmployeeAmount, decimal? OverrideEmployerAmount) : IRequest<Result>;

public sealed class UpdateEmployeeSocialFundEnrollmentCommandHandler : IRequestHandler<UpdateEmployeeSocialFundEnrollmentCommand, Result>
{
    private readonly IEmployeeSocialFundEnrollmentRepository _enrollments;

    public UpdateEmployeeSocialFundEnrollmentCommandHandler(IEmployeeSocialFundEnrollmentRepository enrollments) => _enrollments = enrollments;

    public async Task<Result> Handle(UpdateEmployeeSocialFundEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollments.GetByIdAsync(request.Id, cancellationToken);
        if (enrollment is null)
            return Result.Failure(Error.NotFound("EmployeeSocialFundEnrollment", request.Id));

        var update = enrollment.Update(request.EndDate, request.OverrideEmployeeAmount, request.OverrideEmployerAmount);
        if (update.IsFailure)
            return update;

        await _enrollments.UpdateAsync(enrollment, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteEmployeeSocialFundEnrollmentCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteEmployeeSocialFundEnrollmentCommandHandler : IRequestHandler<DeleteEmployeeSocialFundEnrollmentCommand, Result>
{
    private readonly IEmployeeSocialFundEnrollmentRepository _enrollments;

    public DeleteEmployeeSocialFundEnrollmentCommandHandler(IEmployeeSocialFundEnrollmentRepository enrollments) => _enrollments = enrollments;

    public async Task<Result> Handle(DeleteEmployeeSocialFundEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollments.GetByIdAsync(request.Id, cancellationToken);
        if (enrollment is null)
            return Result.Failure(Error.NotFound("EmployeeSocialFundEnrollment", request.Id));

        await _enrollments.DeleteAsync(enrollment, cancellationToken);
        return Result.Success();
    }
}
