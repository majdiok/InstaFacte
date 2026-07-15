using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Commands;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

public sealed record GetJournalEntryTemplatesQuery(bool? ActiveOnly = true, string? Search = null)
    : IRequest<Result<IReadOnlyList<JournalEntryTemplateDto>>>;

public sealed class GetJournalEntryTemplatesQueryHandler
    : IRequestHandler<GetJournalEntryTemplatesQuery, Result<IReadOnlyList<JournalEntryTemplateDto>>>
{
    private readonly IJournalEntryTemplateRepository _repository;

    public GetJournalEntryTemplatesQueryHandler(IJournalEntryTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<JournalEntryTemplateDto>>> Handle(
        GetJournalEntryTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.GetAllAsync(request.ActiveOnly, request.Search, cancellationToken);
        IReadOnlyList<JournalEntryTemplateDto> result = templates.Select(MapToDto).ToList();
        return Result.Success(result);
    }

    internal static JournalEntryTemplateDto MapToDto(JournalEntryTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        JournalCode = t.JournalCode,
        LabelTemplate = t.LabelTemplate,
        IsActive = t.IsActive,
        UsageCount = t.UsageCount,
        RecurrenceFrequency = (int)t.RecurrenceFrequency,
        RecurrenceDayOfMonth = t.RecurrenceDayOfMonth,
        RecurrenceStartDate = t.RecurrenceStartDate,
        RecurrenceEndDate = t.RecurrenceEndDate,
        NextRunDate = t.NextRunDate,
        LastRunAt = t.LastRunAt,
        IsRecurring = t.IsRecurring,
        Lines = t.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new JournalEntryTemplateLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                AccountNumber = l.AccountNumber,
                LineLabelTemplate = l.LineLabelTemplate,
                FixedDebit = l.FixedDebit,
                FixedCredit = l.FixedCredit
            })
            .ToList()
    };
}

public sealed record GetJournalEntryTemplateByIdQuery(Guid Id)
    : IRequest<Result<JournalEntryTemplateDto>>;

public sealed class GetJournalEntryTemplateByIdQueryHandler
    : IRequestHandler<GetJournalEntryTemplateByIdQuery, Result<JournalEntryTemplateDto>>
{
    private readonly IJournalEntryTemplateRepository _repository;

    public GetJournalEntryTemplateByIdQueryHandler(IJournalEntryTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<JournalEntryTemplateDto>> Handle(
        GetJournalEntryTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return Result.Failure<JournalEntryTemplateDto>(Error.Validation("Id", "Modèle introuvable"));
        return Result.Success(GetJournalEntryTemplatesQueryHandler.MapToDto(template));
    }
}

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

public sealed record CreateJournalEntryTemplateCommand(CreateJournalEntryTemplateRequest Request)
    : IRequest<Result<Guid>>;

public sealed class CreateJournalEntryTemplateCommandHandler
    : IRequestHandler<CreateJournalEntryTemplateCommand, Result<Guid>>
{
    private readonly IJournalEntryTemplateRepository _repository;
    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public CreateJournalEntryTemplateCommandHandler(
        IJournalEntryTemplateRepository repository,
        IChartOfAccountRepository chartOfAccounts,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _chartOfAccounts = chartOfAccounts;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        CreateJournalEntryTemplateCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        if (r.Lines is null || r.Lines.Count < 2)
            return Result.Failure<Guid>(Error.Validation("Lines", "Au moins deux lignes sont requises pour un modèle"));

        // Validate that all referenced accounts exist (skip the active check — a template may
        // reference an account that is temporarily disabled; the validation is done at apply time).
        var checkedAccounts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in r.Lines)
        {
            var acc = line.AccountNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(acc) || !checkedAccounts.Add(acc))
                continue;

            var account = await _chartOfAccounts.GetByAccountNumberAsync(acc, cancellationToken);
            if (account is null)
                return Result.Failure<Guid>(Error.Validation("AccountNumber",
                    $"Le compte {acc} n'existe pas dans le plan comptable."));
        }

        if (await _repository.NameExistsAsync(r.Name, null, cancellationToken))
            return Result.Failure<Guid>(Error.Validation("Name",
                $"Un modèle nommé « {r.Name.Trim()} » existe déjà."));

        var create = JournalEntryTemplate.Create(r.Name, r.JournalCode, r.Description, r.LabelTemplate);
        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var template = create.Value;
        int sort = 0;
        foreach (var l in r.Lines)
        {
            var lineCreate = JournalEntryTemplateLine.Create(
                template,
                l.LineNumber > 0 ? l.LineNumber : ++sort,
                l.AccountNumber,
                l.LineLabelTemplate,
                l.FixedDebit,
                l.FixedCredit);
            if (lineCreate.IsFailure)
                return Result.Failure<Guid>(lineCreate.Error);
            template.AddLine(lineCreate.Value);
        }

        var recurrence = ApplyRecurrence(template, r.RecurrenceFrequency, r.RecurrenceDayOfMonth, r.RecurrenceStartDate, r.RecurrenceEndDate);
        if (recurrence.IsFailure)
            return Result.Failure<Guid>(recurrence.Error);

        template.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _repository.AddAsync(template, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.JournalTemplateCreated,
            "JournalEntryTemplate",
            template.Id,
            newValues: new { template.Name, template.JournalCode, LineCount = r.Lines.Count, template.RecurrenceFrequency },
            cancellationToken: cancellationToken);

        return Result.Success(template.Id);
    }

    /// <summary>
    /// Applique (ou retire) la récurrence demandée sur un modèle dont les lignes sont déjà posées.
    /// Partagé entre la création et la mise à jour.
    /// </summary>
    internal static Result ApplyRecurrence(
        JournalEntryTemplate template,
        int frequency,
        int? dayOfMonth,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (frequency <= 0)
        {
            template.DisableRecurrence();
            return Result.Success();
        }

        if (!Enum.IsDefined(typeof(Domain.Enums.RecurrenceFrequency), frequency))
            return Result.Failure(Error.Validation("Recurrence", "Fréquence de récurrence inconnue."));
        if (dayOfMonth is null || startDate is null)
            return Result.Failure(Error.Validation("Recurrence", "Le jour d'échéance et la date de début sont obligatoires pour une récurrence."));

        return template.ConfigureRecurrence(
            (Domain.Enums.RecurrenceFrequency)frequency, dayOfMonth.Value, startDate.Value, endDate);
    }
}

public sealed record UpdateJournalEntryTemplateCommand(Guid Id, UpdateJournalEntryTemplateRequest Request)
    : IRequest<Result>;

public sealed class UpdateJournalEntryTemplateCommandHandler
    : IRequestHandler<UpdateJournalEntryTemplateCommand, Result>
{
    private readonly IJournalEntryTemplateRepository _repository;
    private readonly IChartOfAccountRepository _chartOfAccounts;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public UpdateJournalEntryTemplateCommandHandler(
        IJournalEntryTemplateRepository repository,
        IChartOfAccountRepository chartOfAccounts,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _chartOfAccounts = chartOfAccounts;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateJournalEntryTemplateCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        if (r.Lines is null || r.Lines.Count < 2)
            return Result.Failure(Error.Validation("Lines", "Au moins deux lignes sont requises pour un modèle"));

        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return Result.Failure(Error.Validation("Id", "Modèle introuvable"));

        var checkedAccounts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in r.Lines)
        {
            var acc = line.AccountNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(acc) || !checkedAccounts.Add(acc))
                continue;

            var account = await _chartOfAccounts.GetByAccountNumberAsync(acc, cancellationToken);
            if (account is null)
                return Result.Failure(Error.Validation("AccountNumber",
                    $"Le compte {acc} n'existe pas dans le plan comptable."));
        }

        if (await _repository.NameExistsAsync(r.Name, request.Id, cancellationToken))
            return Result.Failure(Error.Validation("Name",
                $"Un autre modèle nommé « {r.Name.Trim()} » existe déjà."));

        var u = template.Update(r.Name, r.JournalCode, r.Description, r.LabelTemplate);
        if (u.IsFailure) return u;

        if (r.IsActive.HasValue)
        {
            if (r.IsActive.Value) template.Reactivate();
            else template.Deactivate();
        }

        template.ClearLines();
        int sort = 0;
        foreach (var l in r.Lines)
        {
            var lineCreate = JournalEntryTemplateLine.Create(
                template,
                l.LineNumber > 0 ? l.LineNumber : ++sort,
                l.AccountNumber,
                l.LineLabelTemplate,
                l.FixedDebit,
                l.FixedCredit);
            if (lineCreate.IsFailure)
                return lineCreate;
            template.AddLine(lineCreate.Value);
        }

        var recurrence = CreateJournalEntryTemplateCommandHandler.ApplyRecurrence(
            template, r.RecurrenceFrequency, r.RecurrenceDayOfMonth, r.RecurrenceStartDate, r.RecurrenceEndDate);
        if (recurrence.IsFailure)
            return recurrence;

        template.SetAuditInfo(_currentUser.Email ?? "system", true);
        await _repository.UpdateAsync(template, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.JournalTemplateUpdated,
            "JournalEntryTemplate",
            template.Id,
            newValues: new { template.Name, template.JournalCode, LineCount = r.Lines.Count, r.IsActive },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record RunTemplateRecurrenceCommand(Guid Id) : IRequest<Result<Guid>>;

public sealed class RunTemplateRecurrenceCommandHandler
    : IRequestHandler<RunTemplateRecurrenceCommand, Result<Guid>>
{
    private readonly IRecurringEntryService _recurring;
    private readonly IAuditService _auditService;

    public RunTemplateRecurrenceCommandHandler(IRecurringEntryService recurring, IAuditService auditService)
    {
        _recurring = recurring;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(RunTemplateRecurrenceCommand request, CancellationToken cancellationToken)
    {
        var result = await _recurring.GenerateNowAsync(request.Id, cancellationToken);
        if (result.IsSuccess)
        {
            await _auditService.LogAsync(
                AuditActions.Accounting.RecurringEntryGenerated,
                "JournalEntryTemplate",
                request.Id,
                newValues: new { EntryId = result.Value },
                cancellationToken: cancellationToken);
        }

        return result;
    }
}

public sealed record DeleteJournalEntryTemplateCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteJournalEntryTemplateCommandHandler
    : IRequestHandler<DeleteJournalEntryTemplateCommand, Result>
{
    private readonly IJournalEntryTemplateRepository _repository;
    private readonly IAuditService _auditService;

    public DeleteJournalEntryTemplateCommandHandler(
        IJournalEntryTemplateRepository repository,
        IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeleteJournalEntryTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
            return Result.Failure(Error.Validation("Id", "Modèle introuvable"));

        await _repository.DeleteAsync(template, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.JournalTemplateDeleted,
            "JournalEntryTemplate",
            template.Id,
            oldValues: new { template.Name, template.JournalCode },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
