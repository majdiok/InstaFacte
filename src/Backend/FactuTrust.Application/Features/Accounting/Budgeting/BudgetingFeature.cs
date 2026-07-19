using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.Budgeting;

// Comptabilité budgétaire : postes (catalogue), grille annuelle mensualisée (Initial/Révisé)
// et validation du budget initial. Toute la feature est gardée par AccountingSettings.BudgetingEnabled.

internal static class BudgetingGuard
{
    public static Error Disabled { get; } =
        Error.Validation("Budgeting", "La comptabilité budgétaire n'est pas activée.");
}

// ── Queries ────────────────────────────────────────────────────────────────

public sealed record GetBudgetPostsQuery(bool IncludeInactive) : IRequest<Result<IReadOnlyList<BudgetPostDto>>>;

public sealed class GetBudgetPostsQueryHandler : IRequestHandler<GetBudgetPostsQuery, Result<IReadOnlyList<BudgetPostDto>>>
{
    private readonly IBudgetRepository _budgets;
    private readonly AccountingSettings _settings;

    public GetBudgetPostsQueryHandler(IBudgetRepository budgets, IOptions<AccountingSettings> settings)
    {
        _budgets = budgets;
        _settings = settings.Value;
    }

    public async Task<Result<IReadOnlyList<BudgetPostDto>>> Handle(GetBudgetPostsQuery request, CancellationToken cancellationToken)
    {
        if (!_settings.BudgetingEnabled)
            return Result.Failure<IReadOnlyList<BudgetPostDto>>(BudgetingGuard.Disabled);

        var posts = await _budgets.GetPostsAsync(request.IncludeInactive, cancellationToken);
        var dtos = posts.Select(MapPost).ToList();
        return Result.Success<IReadOnlyList<BudgetPostDto>>(dtos);
    }

    internal static BudgetPostDto MapPost(BudgetPost p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Label = p.Label,
        Kind = (int)p.Kind,
        AccountPrefixes = p.AccountPrefixes,
        IsActive = p.IsActive,
        DisplayOrder = p.DisplayOrder
    };
}

public sealed record GetBudgetYearQuery(int FiscalYear) : IRequest<Result<BudgetYearGridDto>>;

public sealed class GetBudgetYearQueryHandler : IRequestHandler<GetBudgetYearQuery, Result<BudgetYearGridDto>>
{
    private readonly IBudgetRepository _budgets;
    private readonly AccountingSettings _settings;

    public GetBudgetYearQueryHandler(IBudgetRepository budgets, IOptions<AccountingSettings> settings)
    {
        _budgets = budgets;
        _settings = settings.Value;
    }

    public async Task<Result<BudgetYearGridDto>> Handle(GetBudgetYearQuery request, CancellationToken cancellationToken)
    {
        if (!_settings.BudgetingEnabled)
            return Result.Failure<BudgetYearGridDto>(BudgetingGuard.Disabled);
        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure<BudgetYearGridDto>(Error.Validation("FiscalYear", "Exercice invalide."));

        var posts = await _budgets.GetPostsAsync(includeInactive: false, cancellationToken);
        var year = await _budgets.GetYearAsync(request.FiscalYear, cancellationToken);
        var lines = await _budgets.GetLinesAsync(request.FiscalYear, cancellationToken);

        var byPost = lines.ToLookup(l => l.BudgetPostId);
        var rows = posts.Select(p =>
        {
            var initial = new decimal[12];
            var revised = new decimal[12];
            foreach (var line in byPost[p.Id])
            {
                if (line.Version == BudgetVersion.Initial) initial[line.Month - 1] = line.Amount;
                else revised[line.Month - 1] = line.Amount;
            }
            return new BudgetGridRowDto
            {
                BudgetPostId = p.Id,
                Code = p.Code,
                Label = p.Label,
                Kind = (int)p.Kind,
                InitialMonths = initial,
                RevisedMonths = revised
            };
        }).ToList();

        var status = year?.Status ?? BudgetYearStatus.Draft;
        return Result.Success(new BudgetYearGridDto
        {
            FiscalYear = request.FiscalYear,
            Status = (int)status,
            ValidatedAt = year?.ValidatedAt,
            ValidatedBy = year?.ValidatedBy,
            EditableVersion = (int)(year?.EditableVersion ?? BudgetVersion.Initial),
            Rows = rows
        });
    }
}

public sealed record GetBudgetReportQuery(int FiscalYear, int? ThroughMonth) : IRequest<Result<BudgetReportDto>>;

public sealed class GetBudgetReportQueryHandler : IRequestHandler<GetBudgetReportQuery, Result<BudgetReportDto>>
{
    private readonly IAccountingReportingService _reporting;

    public GetBudgetReportQueryHandler(IAccountingReportingService reporting) => _reporting = reporting;

    public Task<Result<BudgetReportDto>> Handle(GetBudgetReportQuery request, CancellationToken cancellationToken)
        => _reporting.GetBudgetReportAsync(request.FiscalYear, request.ThroughMonth, cancellationToken);
}

public sealed record ExportBudgetReportQuery(int FiscalYear, int? ThroughMonth, string Format) : IRequest<Result<byte[]>>;

public sealed class ExportBudgetReportQueryHandler : IRequestHandler<ExportBudgetReportQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;

    public ExportBudgetReportQueryHandler(IAccountingReportingService reporting, IAccountingExportService export)
    {
        _reporting = reporting;
        _export = export;
    }

    public async Task<Result<byte[]>> Handle(ExportBudgetReportQuery request, CancellationToken cancellationToken)
    {
        var report = await _reporting.GetBudgetReportAsync(request.FiscalYear, request.ThroughMonth, cancellationToken);
        if (report.IsFailure)
            return Result.Failure<byte[]>(report.Error);

        return string.Equals(request.Format, "excel", StringComparison.OrdinalIgnoreCase)
            ? Result.Success(_export.ExportBudgetReportToExcel(report.Value))
            : Result.Success(_export.ExportBudgetReportToCsv(report.Value));
    }
}

// ── Commands : postes ──────────────────────────────────────────────────────

public sealed record CreateBudgetPostCommand(CreateBudgetPostRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateBudgetPostCommandHandler : IRequestHandler<CreateBudgetPostCommand, Result<Guid>>
{
    private readonly IBudgetRepository _budgets;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public CreateBudgetPostCommandHandler(IBudgetRepository budgets, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _budgets = budgets;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<Guid>> Handle(CreateBudgetPostCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.BudgetingEnabled)
            return Result.Failure<Guid>(BudgetingGuard.Disabled);

        var r = request.Request;
        var existing = await _budgets.GetPostByCodeAsync(r.Code ?? string.Empty, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(Error.Conflict($"Le poste budgétaire {r.Code} existe déjà."));

        var create = BudgetPost.Create(r.Code ?? string.Empty, r.Label, (BudgetPostKind)r.Kind, r.AccountPrefixes, r.DisplayOrder);
        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var post = create.Value;
        post.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _budgets.AddPostAsync(post, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.BudgetPostCreated, "BudgetPost", post.Id,
            newValues: new { post.Code, post.Label, post.AccountPrefixes }, cancellationToken: cancellationToken);

        return Result.Success(post.Id);
    }
}

public sealed record UpdateBudgetPostCommand(Guid Id, UpdateBudgetPostRequest Request) : IRequest<Result>;

public sealed class UpdateBudgetPostCommandHandler : IRequestHandler<UpdateBudgetPostCommand, Result>
{
    private readonly IBudgetRepository _budgets;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public UpdateBudgetPostCommandHandler(IBudgetRepository budgets, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _budgets = budgets;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(UpdateBudgetPostCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.BudgetingEnabled)
            return Result.Failure(BudgetingGuard.Disabled);

        var post = await _budgets.GetPostByIdAsync(request.Id, cancellationToken);
        if (post is null)
            return Result.Failure(Error.NotFound("BudgetPost", request.Id));

        var r = request.Request;
        var result = post.Update(r.Label, (BudgetPostKind)r.Kind, r.AccountPrefixes, r.DisplayOrder);
        if (result.IsFailure)
            return result;

        post.SetAuditInfo(_currentUser.Email ?? "system", isUpdate: true);
        await _budgets.UpdatePostAsync(post, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.BudgetPostUpdated, "BudgetPost", post.Id,
            newValues: new { post.Code, post.Label, post.AccountPrefixes }, cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record ToggleBudgetPostCommand(Guid Id) : IRequest<Result>;

public sealed class ToggleBudgetPostCommandHandler : IRequestHandler<ToggleBudgetPostCommand, Result>
{
    private readonly IBudgetRepository _budgets;
    private readonly IAuditService _auditService;
    private readonly AccountingSettings _settings;

    public ToggleBudgetPostCommandHandler(IBudgetRepository budgets, IAuditService auditService, IOptions<AccountingSettings> settings)
    {
        _budgets = budgets;
        _auditService = auditService;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(ToggleBudgetPostCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.BudgetingEnabled)
            return Result.Failure(BudgetingGuard.Disabled);

        var post = await _budgets.GetPostByIdAsync(request.Id, cancellationToken);
        if (post is null)
            return Result.Failure(Error.NotFound("BudgetPost", request.Id));

        post.ToggleActive();
        await _budgets.UpdatePostAsync(post, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.BudgetPostToggled, "BudgetPost", post.Id,
            newValues: new { post.Code, post.IsActive }, cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ── Commands : grille + validation ─────────────────────────────────────────

public sealed record SaveBudgetYearCommand(int FiscalYear, SaveBudgetYearRequest Request) : IRequest<Result>;

public sealed class SaveBudgetYearCommandHandler : IRequestHandler<SaveBudgetYearCommand, Result>
{
    private readonly IBudgetRepository _budgets;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public SaveBudgetYearCommandHandler(IBudgetRepository budgets, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _budgets = budgets;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(SaveBudgetYearCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.BudgetingEnabled)
            return Result.Failure(BudgetingGuard.Disabled);
        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure(Error.Validation("FiscalYear", "Exercice invalide."));
        if (request.Request.Lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Aucune ligne budgétaire fournie."));

        // La version écrite est décidée par le statut de l'exercice, jamais par le client.
        var year = await _budgets.GetYearAsync(request.FiscalYear, cancellationToken);
        var version = year?.EditableVersion ?? BudgetVersion.Initial;

        var user = _currentUser.Email ?? "system";
        var lines = request.Request.Lines
            .Select(l => (l.BudgetPostId, l.Month, l.Amount))
            .ToList();

        var result = await _budgets.UpsertYearLinesAsync(request.FiscalYear, version, lines, user, cancellationToken);
        if (result.IsFailure)
            return result;

        await _auditService.LogAsync(AuditActions.Accounting.BudgetYearSaved, "BudgetYear", null,
            newValues: new { request.FiscalYear, Version = version.ToString(), LineCount = lines.Count },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record ValidateInitialBudgetCommand(int FiscalYear) : IRequest<Result>;

public sealed class ValidateInitialBudgetCommandHandler : IRequestHandler<ValidateInitialBudgetCommand, Result>
{
    private readonly IBudgetRepository _budgets;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public ValidateInitialBudgetCommandHandler(IBudgetRepository budgets, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _budgets = budgets;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(ValidateInitialBudgetCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAccountingFirmDelegatedContext)
            return Result.Failure(Error.Forbidden(AccountingValidationAccess.DeniedMessage));

        if (!_settings.BudgetingEnabled)
            return Result.Failure(BudgetingGuard.Disabled);
        if (request.FiscalYear is < 2000 or > 2100)
            return Result.Failure(Error.Validation("FiscalYear", "Exercice invalide."));

        var user = _currentUser.Email ?? "system";
        var result = await _budgets.ValidateInitialAsync(request.FiscalYear, user, cancellationToken);
        if (result.IsFailure)
            return result;

        await _auditService.LogAsync(AuditActions.Accounting.BudgetInitialValidated, "BudgetYear", null,
            newValues: new { request.FiscalYear, ValidatedBy = user }, cancellationToken: cancellationToken);

        return Result.Success();
    }
}
