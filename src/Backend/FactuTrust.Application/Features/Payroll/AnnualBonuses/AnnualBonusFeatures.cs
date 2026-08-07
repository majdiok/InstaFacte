using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.AnnualBonuses;

public sealed record ListAnnualBonusRulesQuery(int? FiscalYear = null) : IRequest<IReadOnlyList<AnnualBonusRuleDto>>;

public sealed class ListAnnualBonusRulesQueryHandler : IRequestHandler<ListAnnualBonusRulesQuery, IReadOnlyList<AnnualBonusRuleDto>>
{
    private readonly IAnnualBonusRuleRepository _rules;

    public ListAnnualBonusRulesQueryHandler(IAnnualBonusRuleRepository rules) => _rules = rules;

    public async Task<IReadOnlyList<AnnualBonusRuleDto>> Handle(ListAnnualBonusRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _rules.ListAsync(request.FiscalYear, cancellationToken);
        return rules.Select(AnnualBonusMappings.ToDto).ToList();
    }
}

public sealed record CreateAnnualBonusRuleCommand(UpsertAnnualBonusRuleDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateAnnualBonusRuleCommandValidator : AbstractValidator<CreateAnnualBonusRuleCommand>
{
    public CreateAnnualBonusRuleCommandValidator()
    {
        RuleFor(x => x.Dto.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Dto.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.PaymentMonth).InclusiveBetween(1, 12);
    }
}

public sealed class CreateAnnualBonusRuleCommandHandler : IRequestHandler<CreateAnnualBonusRuleCommand, Result<Guid>>
{
    private readonly IAnnualBonusRuleRepository _rules;
    private readonly AccountingSettings _settings;

    public CreateAnnualBonusRuleCommandHandler(IAnnualBonusRuleRepository rules, IOptions<AccountingSettings> settings)
    {
        _rules = rules;
        _settings = settings.Value;
    }

    public async Task<Result<Guid>> Handle(CreateAnnualBonusRuleCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.PayrollAnnualBonusesEnabled)
            return Result.Failure<Guid>(Error.Validation("Feature", "Les primes annuelles ne sont pas activées."));

        var dto = request.Dto;
        if (!Enum.TryParse<AnnualBonusKind>(dto.Kind, out var kind))
            return Result.Failure<Guid>(Error.Validation("Kind", "Type de prime invalide."));
        if (!Enum.TryParse<AnnualBonusFormula>(dto.Formula, out var formula))
            return Result.Failure<Guid>(Error.Validation("Formula", "Formule invalide."));

        if (await _rules.GetByCodeAsync(dto.Code, cancellationToken) is not null)
            return Result.Failure<Guid>(Error.Validation("Code", "Ce code existe déjà."));

        var result = AnnualBonusRule.Create(
            dto.Code, dto.Label, kind, formula, dto.PaymentMonth,
            dto.FixedAmount, dto.RatePercent, dto.MonthsOfBase,
            dto.Taxable, dto.SubjectToCnss, dto.FiscalYear);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        var entity = result.Value;
        if (!dto.IsActive)
            entity.Update(dto.Label, kind, formula, dto.PaymentMonth, dto.FixedAmount, dto.RatePercent,
                dto.MonthsOfBase, dto.Taxable, dto.SubjectToCnss, false, dto.FiscalYear);

        await _rules.AddAsync(entity, cancellationToken);
        return Result.Success(entity.Id);
    }
}

public sealed record UpdateAnnualBonusRuleCommand(Guid Id, UpsertAnnualBonusRuleDto Dto) : IRequest<Result>;

public sealed class UpdateAnnualBonusRuleCommandHandler : IRequestHandler<UpdateAnnualBonusRuleCommand, Result>
{
    private readonly IAnnualBonusRuleRepository _rules;
    private readonly AccountingSettings _settings;

    public UpdateAnnualBonusRuleCommandHandler(IAnnualBonusRuleRepository rules, IOptions<AccountingSettings> settings)
    {
        _rules = rules;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(UpdateAnnualBonusRuleCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.PayrollAnnualBonusesEnabled)
            return Result.Failure(Error.Validation("Feature", "Les primes annuelles ne sont pas activées."));

        var rule = await _rules.GetByIdAsync(request.Id, cancellationToken);
        if (rule is null)
            return Result.Failure(Error.NotFound("AnnualBonusRule", request.Id));

        var dto = request.Dto;
        if (!Enum.TryParse<AnnualBonusKind>(dto.Kind, out var kind))
            return Result.Failure(Error.Validation("Kind", "Type de prime invalide."));
        if (!Enum.TryParse<AnnualBonusFormula>(dto.Formula, out var formula))
            return Result.Failure(Error.Validation("Formula", "Formule invalide."));

        var update = rule.Update(
            dto.Label, kind, formula, dto.PaymentMonth,
            dto.FixedAmount, dto.RatePercent, dto.MonthsOfBase,
            dto.Taxable, dto.SubjectToCnss, dto.IsActive, dto.FiscalYear);

        if (update.IsFailure)
            return update;

        await _rules.UpdateAsync(rule, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteAnnualBonusRuleCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteAnnualBonusRuleCommandHandler : IRequestHandler<DeleteAnnualBonusRuleCommand, Result>
{
    private readonly IAnnualBonusRuleRepository _rules;

    public DeleteAnnualBonusRuleCommandHandler(IAnnualBonusRuleRepository rules) => _rules = rules;

    public async Task<Result> Handle(DeleteAnnualBonusRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _rules.GetByIdAsync(request.Id, cancellationToken);
        if (rule is null)
            return Result.Failure(Error.NotFound("AnnualBonusRule", request.Id));

        await _rules.DeleteAsync(rule, cancellationToken);
        return Result.Success();
    }
}

public sealed record ListEmployeeAnnualBonusRulesQuery(Guid? EmployeeId = null)
    : IRequest<IReadOnlyList<EmployeeAnnualBonusRuleDto>>;

public sealed class ListEmployeeAnnualBonusRulesQueryHandler
    : IRequestHandler<ListEmployeeAnnualBonusRulesQuery, IReadOnlyList<EmployeeAnnualBonusRuleDto>>
{
    private readonly IEmployeeAnnualBonusRuleRepository _assignments;
    private readonly IAnnualBonusRuleRepository _rules;
    private readonly IEmployeeRepository _employees;

    public ListEmployeeAnnualBonusRulesQueryHandler(
        IEmployeeAnnualBonusRuleRepository assignments,
        IAnnualBonusRuleRepository rules,
        IEmployeeRepository employees)
    {
        _assignments = assignments;
        _rules = rules;
        _employees = employees;
    }

    public async Task<IReadOnlyList<EmployeeAnnualBonusRuleDto>> Handle(
        ListEmployeeAnnualBonusRulesQuery request,
        CancellationToken cancellationToken)
    {
        var items = request.EmployeeId.HasValue
            ? await _assignments.ListByEmployeeAsync(request.EmployeeId.Value, cancellationToken)
            : await _assignments.ListActiveAsync(cancellationToken);

        if (items.Count == 0)
            return Array.Empty<EmployeeAnnualBonusRuleDto>();

        var ruleLabels = (await _rules.ListAsync(cancellationToken: cancellationToken))
            .ToDictionary(r => r.Id, r => r.Label);
        var names = await _employees.GetFullNamesByIdsAsync(
            items.Select(a => a.EmployeeId).Distinct().ToList(), cancellationToken);

        return items.Select(a => AnnualBonusMappings.ToEmployeeDto(
            a, names.GetValueOrDefault(a.EmployeeId), ruleLabels.GetValueOrDefault(a.AnnualBonusRuleId))).ToList();
    }
}

public sealed record UpsertEmployeeAnnualBonusRuleCommand(UpsertEmployeeAnnualBonusRuleDto Dto) : IRequest<Result<Guid>>;

public sealed class UpsertEmployeeAnnualBonusRuleCommandHandler
    : IRequestHandler<UpsertEmployeeAnnualBonusRuleCommand, Result<Guid>>
{
    private readonly IEmployeeAnnualBonusRuleRepository _assignments;
    private readonly IEmployeeRepository _employees;
    private readonly IAnnualBonusRuleRepository _rules;

    public UpsertEmployeeAnnualBonusRuleCommandHandler(
        IEmployeeAnnualBonusRuleRepository assignments,
        IEmployeeRepository employees,
        IAnnualBonusRuleRepository rules)
    {
        _assignments = assignments;
        _employees = employees;
        _rules = rules;
    }

    public async Task<Result<Guid>> Handle(UpsertEmployeeAnnualBonusRuleCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (!await _employees.ExistsAsync(dto.EmployeeId, cancellationToken))
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));
        if (await _rules.GetByIdAsync(dto.AnnualBonusRuleId, cancellationToken) is null)
            return Result.Failure<Guid>(Error.NotFound("AnnualBonusRule", dto.AnnualBonusRuleId));

        var existing = await _assignments.GetByEmployeeAndRuleAsync(
            dto.EmployeeId, dto.AnnualBonusRuleId, cancellationToken);

        if (existing is not null)
        {
            var update = existing.Update(dto.IsActive, dto.OverrideFixedAmount, dto.OverrideRatePercent, dto.OverrideMonthsOfBase);
            if (update.IsFailure)
                return Result.Failure<Guid>(update.Error);
            await _assignments.UpdateAsync(existing, cancellationToken);
            return Result.Success(existing.Id);
        }

        var create = EmployeeAnnualBonusRule.Create(
            dto.EmployeeId, dto.AnnualBonusRuleId,
            dto.OverrideFixedAmount, dto.OverrideRatePercent, dto.OverrideMonthsOfBase);
        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        if (!dto.IsActive)
            create.Value.Update(false, dto.OverrideFixedAmount, dto.OverrideRatePercent, dto.OverrideMonthsOfBase);

        await _assignments.AddAsync(create.Value, cancellationToken);
        return Result.Success(create.Value.Id);
    }
}

internal static class AnnualBonusMappings
{
    public static AnnualBonusRuleDto ToDto(AnnualBonusRule r) => new()
    {
        Id = r.Id,
        Code = r.Code,
        Label = r.Label,
        Kind = r.Kind.ToString(),
        KindDisplay = r.Kind.ToDisplayString(),
        Formula = r.Formula.ToString(),
        FormulaDisplay = r.Formula.ToDisplayString(),
        PaymentMonth = r.PaymentMonth,
        FixedAmount = r.FixedAmount,
        RatePercent = r.RatePercent,
        MonthsOfBase = r.MonthsOfBase,
        Taxable = r.Taxable,
        SubjectToCnss = r.SubjectToCnss,
        IsActive = r.IsActive,
        FiscalYear = r.FiscalYear
    };

    public static EmployeeAnnualBonusRuleDto ToEmployeeDto(
        EmployeeAnnualBonusRule a, string? employeeName, string? ruleLabel) => new()
    {
        Id = a.Id,
        EmployeeId = a.EmployeeId,
        EmployeeName = employeeName,
        AnnualBonusRuleId = a.AnnualBonusRuleId,
        RuleLabel = ruleLabel,
        IsActive = a.IsActive,
        OverrideFixedAmount = a.OverrideFixedAmount,
        OverrideRatePercent = a.OverrideRatePercent,
        OverrideMonthsOfBase = a.OverrideMonthsOfBase
    };
}
