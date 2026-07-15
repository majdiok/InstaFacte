using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.JournalCatalog;

// ── Queries ────────────────────────────────────────────────────────────────

public sealed record GetJournalsQuery(bool IncludeInactive) : IRequest<Result<IReadOnlyList<JournalDto>>>;

public sealed class GetJournalsQueryHandler : IRequestHandler<GetJournalsQuery, Result<IReadOnlyList<JournalDto>>>
{
    private readonly IJournalRepository _journals;

    public GetJournalsQueryHandler(IJournalRepository journals) => _journals = journals;

    public async Task<Result<IReadOnlyList<JournalDto>>> Handle(GetJournalsQuery request, CancellationToken cancellationToken)
    {
        var families = (await _journals.GetAllFamiliesAsync(cancellationToken)).ToDictionary(f => f.Id, f => f.Label);
        var journals = await _journals.GetAllJournalsAsync(request.IncludeInactive, cancellationToken);
        var dtos = journals.Select(j => new JournalDto
        {
            Id = j.Id,
            Code = j.Code,
            Label = j.Label,
            FamilyId = j.FamilyId,
            FamilyLabel = j.FamilyId.HasValue && families.TryGetValue(j.FamilyId.Value, out var lbl) ? lbl : null,
            IsSystem = j.IsSystem,
            IsActive = j.IsActive
        }).ToList();
        return Result.Success<IReadOnlyList<JournalDto>>(dtos);
    }
}

public sealed record GetJournalFamiliesQuery : IRequest<Result<IReadOnlyList<JournalFamilyDto>>>;

public sealed class GetJournalFamiliesQueryHandler : IRequestHandler<GetJournalFamiliesQuery, Result<IReadOnlyList<JournalFamilyDto>>>
{
    private readonly IJournalRepository _journals;

    public GetJournalFamiliesQueryHandler(IJournalRepository journals) => _journals = journals;

    public async Task<Result<IReadOnlyList<JournalFamilyDto>>> Handle(GetJournalFamiliesQuery request, CancellationToken cancellationToken)
    {
        var families = await _journals.GetAllFamiliesAsync(cancellationToken);
        var dtos = families.Select(f => new JournalFamilyDto { Id = f.Id, Code = f.Code, Label = f.Label }).ToList();
        return Result.Success<IReadOnlyList<JournalFamilyDto>>(dtos);
    }
}

// ── Commands ───────────────────────────────────────────────────────────────

public sealed record CreateJournalCommand(CreateJournalRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateJournalCommandHandler : IRequestHandler<CreateJournalCommand, Result<Guid>>
{
    private readonly IJournalRepository _journals;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public CreateJournalCommandHandler(IJournalRepository journals, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _journals = journals;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<Guid>> Handle(CreateJournalCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.JournalCatalogEnabled)
            return Result.Failure<Guid>(Error.Validation("Journal", "La gestion des journaux n'est pas activée."));

        var r = request.Request;
        var existing = await _journals.GetJournalByCodeAsync(r.Code ?? string.Empty, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(Error.Conflict($"Le journal {r.Code} existe déjà."));

        var create = Journal.Create(r.Code ?? string.Empty, r.Label, r.FamilyId, isSystem: false);
        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var journal = create.Value;
        journal.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _journals.AddJournalAsync(journal, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.JournalCreated, "Journal", journal.Id,
            newValues: new { journal.Code, journal.Label }, cancellationToken: cancellationToken);

        return Result.Success(journal.Id);
    }
}

public sealed record UpdateJournalCommand(Guid Id, UpdateJournalRequest Request) : IRequest<Result>;

public sealed class UpdateJournalCommandHandler : IRequestHandler<UpdateJournalCommand, Result>
{
    private readonly IJournalRepository _journals;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public UpdateJournalCommandHandler(IJournalRepository journals, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _journals = journals;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(UpdateJournalCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.JournalCatalogEnabled)
            return Result.Failure(Error.Validation("Journal", "La gestion des journaux n'est pas activée."));

        var journal = await _journals.GetJournalByIdAsync(request.Id, cancellationToken);
        if (journal is null)
            return Result.Failure(Error.NotFound("Journal", request.Id));

        var result = journal.Update(request.Request.Label, request.Request.FamilyId);
        if (result.IsFailure)
            return result;

        journal.SetAuditInfo(_currentUser.Email ?? "system", isUpdate: true);
        await _journals.UpdateJournalAsync(journal, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.JournalUpdated, "Journal", journal.Id,
            newValues: new { journal.Code, journal.Label }, cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record ToggleJournalActiveCommand(Guid Id) : IRequest<Result>;

public sealed class ToggleJournalActiveCommandHandler : IRequestHandler<ToggleJournalActiveCommand, Result>
{
    private readonly IJournalRepository _journals;
    private readonly IAuditService _auditService;
    private readonly AccountingSettings _settings;

    public ToggleJournalActiveCommandHandler(IJournalRepository journals, IAuditService auditService, IOptions<AccountingSettings> settings)
    {
        _journals = journals;
        _auditService = auditService;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(ToggleJournalActiveCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.JournalCatalogEnabled)
            return Result.Failure(Error.Validation("Journal", "La gestion des journaux n'est pas activée."));

        var journal = await _journals.GetJournalByIdAsync(request.Id, cancellationToken);
        if (journal is null)
            return Result.Failure(Error.NotFound("Journal", request.Id));
        if (journal.IsSystem)
            return Result.Failure(Error.Validation("Journal", "Un journal système ne peut pas être désactivé."));

        if (journal.IsActive) journal.Deactivate();
        else journal.Reactivate();
        await _journals.UpdateJournalAsync(journal, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.JournalToggled, "Journal", journal.Id,
            newValues: new { journal.Code, journal.IsActive }, cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record CreateJournalFamilyCommand(CreateJournalFamilyRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateJournalFamilyCommandHandler : IRequestHandler<CreateJournalFamilyCommand, Result<Guid>>
{
    private readonly IJournalRepository _journals;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public CreateJournalFamilyCommandHandler(IJournalRepository journals, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _journals = journals;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<Guid>> Handle(CreateJournalFamilyCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.JournalCatalogEnabled)
            return Result.Failure<Guid>(Error.Validation("Journal", "La gestion des journaux n'est pas activée."));

        var create = JournalFamily.Create(request.Request.Code, request.Request.Label);
        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var family = create.Value;
        family.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _journals.AddFamilyAsync(family, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.JournalFamilyCreated, "JournalFamily", family.Id,
            newValues: new { family.Code, family.Label }, cancellationToken: cancellationToken);

        return Result.Success(family.Id);
    }
}
