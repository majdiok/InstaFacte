using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.CRM;

// ==================== OPPORTUNITIES ====================

public sealed record GetOpportunitiesQuery(OpportunityStage? Stage, Guid? AssignedUserId, Guid? ClientId) : IRequest<Result<IReadOnlyList<OpportunityDto>>>;

public sealed class GetOpportunitiesQueryHandler : IRequestHandler<GetOpportunitiesQuery, Result<IReadOnlyList<OpportunityDto>>>
{
    private readonly IOpportunityRepository _repo;
    public GetOpportunitiesQueryHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result<IReadOnlyList<OpportunityDto>>> Handle(GetOpportunitiesQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.Stage, request.AssignedUserId, request.ClientId, ct);
        var dtos = items.Select(o => MapOpportunity(o)).ToList();
        return Result.Success<IReadOnlyList<OpportunityDto>>(dtos);
    }

    internal static OpportunityDto MapOpportunity(Opportunity o) => new()
    {
        Id = o.Id,
        Title = o.Title,
        ClientId = o.ClientId,
        ClientName = "",
        AssignedUserId = o.AssignedUserId,
        AssignedUserName = o.AssignedUserName,
        Stage = (int)o.Stage,
        StageName = o.Stage.ToDisplayString(),
        StageColor = o.Stage.ToCssClass(),
        ExpectedAmount = o.ExpectedAmount.Amount,
        Probability = o.Probability,
        WeightedAmount = o.WeightedAmount,
        ExpectedCloseDate = o.ExpectedCloseDate,
        ActualCloseDate = o.ActualCloseDate,
        LostReason = o.LostReason,
        LinkedQuoteId = o.LinkedQuoteId,
        LinkedInvoiceId = o.LinkedInvoiceId,
        Notes = o.Notes,
        Source = o.Source,
        Currency = o.ExpectedAmount.Currency
    };
}

public sealed record GetOpportunityByIdQuery(Guid Id) : IRequest<Result<OpportunityDto>>;

public sealed class GetOpportunityByIdQueryHandler : IRequestHandler<GetOpportunityByIdQuery, Result<OpportunityDto>>
{
    private readonly IOpportunityRepository _repo;
    public GetOpportunityByIdQueryHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result<OpportunityDto>> Handle(GetOpportunityByIdQuery request, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(request.Id, ct);
        if (o is null) return Result.Failure<OpportunityDto>(Error.Validation("Id", "Opportunité introuvable"));
        return Result.Success(GetOpportunitiesQueryHandler.MapOpportunity(o));
    }
}

public sealed record CreateOpportunityCommand(CreateOpportunityRequest Request, Guid CurrentUserId, string CurrentUserName) : IRequest<Result<Guid>>;

public sealed class CreateOpportunityCommandHandler : IRequestHandler<CreateOpportunityCommand, Result<Guid>>
{
    private readonly IOpportunityRepository _repo;
    public CreateOpportunityCommandHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result<Guid>> Handle(CreateOpportunityCommand cmd, CancellationToken ct)
    {
        var r = cmd.Request;
        var create = Opportunity.Create(
            r.Title, r.ClientId, cmd.CurrentUserId, cmd.CurrentUserName,
            Money.Create(r.ExpectedAmount), r.Probability, r.ExpectedCloseDate,
            r.Source, r.Notes);
        if (create.IsFailure) return Result.Failure<Guid>(create.Error);
        var entity = create.Value;
        entity.SetAuditInfo(cmd.CurrentUserId.ToString(), false);
        await _repo.AddAsync(entity, ct);
        return Result.Success(entity.Id);
    }
}

public sealed record UpdateOpportunityCommand(Guid Id, UpdateOpportunityRequest Request) : IRequest<Result>;

public sealed class UpdateOpportunityCommandHandler : IRequestHandler<UpdateOpportunityCommand, Result>
{
    private readonly IOpportunityRepository _repo;
    public UpdateOpportunityCommandHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result> Handle(UpdateOpportunityCommand cmd, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(cmd.Id, ct);
        if (o is null) return Result.Failure(Error.Validation("Id", "Opportunité introuvable"));
        var r = cmd.Request;
        var result = o.Update(r.Title, Money.Create(r.ExpectedAmount), r.Probability, r.ExpectedCloseDate, r.Source, r.Notes);
        if (result.IsFailure) return result;
        await _repo.UpdateAsync(o, ct);
        return Result.Success();
    }
}

public sealed record AdvanceOpportunityCommand(Guid Id) : IRequest<Result>;

public sealed class AdvanceOpportunityCommandHandler : IRequestHandler<AdvanceOpportunityCommand, Result>
{
    private readonly IOpportunityRepository _repo;
    public AdvanceOpportunityCommandHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result> Handle(AdvanceOpportunityCommand cmd, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(cmd.Id, ct);
        if (o is null) return Result.Failure(Error.Validation("Id", "Opportunité introuvable"));
        var result = o.Advance();
        if (result.IsFailure) return result;
        await _repo.UpdateAsync(o, ct);
        return Result.Success();
    }
}

public sealed record WinOpportunityCommand(Guid Id) : IRequest<Result>;

public sealed class WinOpportunityCommandHandler : IRequestHandler<WinOpportunityCommand, Result>
{
    private readonly IOpportunityRepository _repo;
    public WinOpportunityCommandHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result> Handle(WinOpportunityCommand cmd, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(cmd.Id, ct);
        if (o is null) return Result.Failure(Error.Validation("Id", "Opportunité introuvable"));
        var result = o.Win();
        if (result.IsFailure) return result;
        await _repo.UpdateAsync(o, ct);
        return Result.Success();
    }
}

public sealed record LoseOpportunityCommand(Guid Id, string Reason) : IRequest<Result>;

public sealed class LoseOpportunityCommandHandler : IRequestHandler<LoseOpportunityCommand, Result>
{
    private readonly IOpportunityRepository _repo;
    public LoseOpportunityCommandHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result> Handle(LoseOpportunityCommand cmd, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(cmd.Id, ct);
        if (o is null) return Result.Failure(Error.Validation("Id", "Opportunité introuvable"));
        var result = o.Lose(cmd.Reason);
        if (result.IsFailure) return result;
        await _repo.UpdateAsync(o, ct);
        return Result.Success();
    }
}

public sealed record ReopenOpportunityCommand(Guid Id) : IRequest<Result>;

public sealed class ReopenOpportunityCommandHandler : IRequestHandler<ReopenOpportunityCommand, Result>
{
    private readonly IOpportunityRepository _repo;
    public ReopenOpportunityCommandHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result> Handle(ReopenOpportunityCommand cmd, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(cmd.Id, ct);
        if (o is null) return Result.Failure(Error.Validation("Id", "Opportunité introuvable"));
        var result = o.Reopen();
        if (result.IsFailure) return result;
        await _repo.UpdateAsync(o, ct);
        return Result.Success();
    }
}

public sealed record DeleteOpportunityCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteOpportunityCommandHandler : IRequestHandler<DeleteOpportunityCommand, Result>
{
    private readonly IOpportunityRepository _repo;
    public DeleteOpportunityCommandHandler(IOpportunityRepository repo) => _repo = repo;

    public async Task<Result> Handle(DeleteOpportunityCommand cmd, CancellationToken ct)
    {
        var o = await _repo.GetByIdAsync(cmd.Id, ct);
        if (o is null) return Result.Failure(Error.Validation("Id", "Opportunité introuvable"));
        await _repo.DeleteAsync(o, ct);
        return Result.Success();
    }
}

// ==================== ACTIVITIES ====================

public sealed record GetCrmAssignableUsersQuery : IRequest<Result<IReadOnlyList<CrmAssignableUserDto>>>;

public sealed class GetCrmAssignableUsersQueryHandler : IRequestHandler<GetCrmAssignableUsersQuery, Result<IReadOnlyList<CrmAssignableUserDto>>>
{
    private readonly IAssignableTenantUsersSource _source;
    public GetCrmAssignableUsersQueryHandler(IAssignableTenantUsersSource source) => _source = source;

    public async Task<Result<IReadOnlyList<CrmAssignableUserDto>>> Handle(GetCrmAssignableUsersQuery request, CancellationToken ct)
    {
        var list = await _source.ListActiveAsync(ct);
        return Result.Success<IReadOnlyList<CrmAssignableUserDto>>(list);
    }
}

public sealed record GetActivitiesQuery(
    Guid? ClientId,
    Guid? AssignedUserId,
    Guid? OpportunityId,
    bool? Completed,
    DateTime? DueFrom,
    DateTime? DueTo,
    int? ActivityType,
    string? SearchSubject,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<SalesActivityDto>>>;

public sealed class GetActivitiesQueryHandler : IRequestHandler<GetActivitiesQuery, Result<PagedResult<SalesActivityDto>>>
{
    private readonly ISalesActivityRepository _repo;
    private readonly IClientRepository _clients;

    public GetActivitiesQueryHandler(ISalesActivityRepository repo, IClientRepository clients)
    {
        _repo = repo;
        _clients = clients;
    }

    public async Task<Result<PagedResult<SalesActivityDto>>> Handle(GetActivitiesQuery request, CancellationToken ct)
    {
        var (items, total) = await _repo.SearchAsync(
            request.ClientId,
            request.AssignedUserId,
            request.OpportunityId,
            request.Completed,
            request.DueFrom,
            request.DueTo,
            request.ActivityType,
            request.SearchSubject,
            request.Page,
            request.PageSize,
            ct);

        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
        if (items.Count > 0)
        {
            var ids = items.Select(a => a.ClientId).Distinct().ToList();
            names = await _clients.GetDisplayNamesByIdsAsync(ids, ct);
        }

        var dtos = items.Select(a => MapActivity(a, names.GetValueOrDefault(a.ClientId))).ToList();
        return Result.Success(PagedResult<SalesActivityDto>.Create(dtos, request.Page, request.PageSize, total));
    }

    internal static SalesActivityDto MapActivity(SalesActivity a, string? clientName = null) => new()
    {
        Id = a.Id,
        Type = (int)a.Type,
        TypeName = a.Type.ToDisplayString(),
        TypeIcon = a.Type.ToIcon(),
        Subject = a.Subject,
        Description = a.Description,
        ClientId = a.ClientId,
        ClientName = string.IsNullOrEmpty(clientName) ? null : clientName,
        OpportunityId = a.OpportunityId,
        AssignedUserId = a.AssignedUserId,
        AssignedUserName = a.AssignedUserName,
        DueDate = a.DueDate,
        CompletedAt = a.CompletedAt,
        IsCompleted = a.IsCompleted,
        Priority = (int)a.Priority,
        PriorityName = a.Priority.ToDisplayString(),
        PriorityColor = a.Priority.ToCssClass(),
        ReminderDate = a.ReminderDate,
        LinkedEntityType = a.LinkedEntityType,
        LinkedEntityId = a.LinkedEntityId
    };
}

public sealed record GetMyRemindersQuery(Guid UserId) : IRequest<Result<IReadOnlyList<SalesActivityDto>>>;

public sealed class GetMyRemindersQueryHandler : IRequestHandler<GetMyRemindersQuery, Result<IReadOnlyList<SalesActivityDto>>>
{
    private readonly ISalesActivityRepository _repo;
    private readonly IClientRepository _clients;

    public GetMyRemindersQueryHandler(ISalesActivityRepository repo, IClientRepository clients)
    {
        _repo = repo;
        _clients = clients;
    }

    public async Task<Result<IReadOnlyList<SalesActivityDto>>> Handle(GetMyRemindersQuery request, CancellationToken ct)
    {
        var upTo = DateTime.UtcNow.Date.AddDays(7);
        var items = await _repo.GetRemindersAsync(request.UserId, upTo, ct);
        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
        if (items.Count > 0)
        {
            var ids = items.Select(a => a.ClientId).Distinct().ToList();
            names = await _clients.GetDisplayNamesByIdsAsync(ids, ct);
        }

        var dtos = items.Select(a => GetActivitiesQueryHandler.MapActivity(a, names.GetValueOrDefault(a.ClientId))).ToList();
        return Result.Success<IReadOnlyList<SalesActivityDto>>(dtos);
    }
}

public sealed record GetActivitiesSummaryQuery(
    Guid? ClientId,
    Guid? AssignedUserId,
    Guid? OpportunityId,
    bool? Completed,
    DateTime? DueFrom,
    DateTime? DueTo,
    int? ActivityType,
    string? SearchSubject) : IRequest<Result<ActivityListSummaryDto>>;

public sealed class GetActivitiesSummaryQueryHandler : IRequestHandler<GetActivitiesSummaryQuery, Result<ActivityListSummaryDto>>
{
    private readonly ISalesActivityRepository _repo;

    public GetActivitiesSummaryQueryHandler(ISalesActivityRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<ActivityListSummaryDto>> Handle(GetActivitiesSummaryQuery request, CancellationToken ct)
    {
        var summary = await _repo.GetSummaryAsync(
            request.ClientId,
            request.AssignedUserId,
            request.OpportunityId,
            request.Completed,
            request.DueFrom,
            request.DueTo,
            request.ActivityType,
            request.SearchSubject,
            ct);
        return Result.Success(summary);
    }
}

public sealed record CreateActivityCommand(CreateActivityRequest Request, Guid CurrentUserId, string CurrentUserName) : IRequest<Result<Guid>>;

public sealed class CreateActivityCommandHandler : IRequestHandler<CreateActivityCommand, Result<Guid>>
{
    private readonly ISalesActivityRepository _repo;
    private readonly ITenantMemberDirectory _members;

    public CreateActivityCommandHandler(ISalesActivityRepository repo, ITenantMemberDirectory members)
    {
        _repo = repo;
        _members = members;
    }

    public async Task<Result<Guid>> Handle(CreateActivityCommand cmd, CancellationToken ct)
    {
        var r = cmd.Request;
        Guid assigneeId;
        string assigneeName;
        if (!r.AssignedUserId.HasValue || r.AssignedUserId.Value == cmd.CurrentUserId)
        {
            assigneeId = cmd.CurrentUserId;
            assigneeName = cmd.CurrentUserName;
        }
        else
        {
            var m = await _members.GetMemberAsync(r.AssignedUserId.Value, ct);
            if (m.IsFailure) return Result.Failure<Guid>(m.Error);
            assigneeId = m.Value.Id;
            assigneeName = m.Value.DisplayName;
        }

        var create = SalesActivity.Create(
            (ActivityType)r.Type, r.Subject, r.ClientId,
            assigneeId, assigneeName,
            (ActivityPriority)r.Priority, r.Description,
            r.OpportunityId, r.DueDate, r.ReminderDate,
            r.LinkedEntityType, r.LinkedEntityId);
        if (create.IsFailure) return Result.Failure<Guid>(create.Error);
        var entity = create.Value;
        entity.SetAuditInfo(cmd.CurrentUserId.ToString(), false);
        await _repo.AddAsync(entity, ct);
        return Result.Success(entity.Id);
    }
}

public sealed record UpdateActivityCommand(Guid Id, UpdateActivityRequest Request) : IRequest<Result>;

public sealed class UpdateActivityCommandHandler : IRequestHandler<UpdateActivityCommand, Result>
{
    private readonly ISalesActivityRepository _repo;
    private readonly ITenantMemberDirectory _members;

    public UpdateActivityCommandHandler(ISalesActivityRepository repo, ITenantMemberDirectory members)
    {
        _repo = repo;
        _members = members;
    }

    public async Task<Result> Handle(UpdateActivityCommand cmd, CancellationToken ct)
    {
        var a = await _repo.GetByIdAsync(cmd.Id, ct);
        if (a is null) return Result.Failure(Error.Validation("Id", "Activité introuvable"));
        var r = cmd.Request;
        var result = a.Update((ActivityType)r.Type, r.Subject, r.Description, (ActivityPriority)r.Priority, r.DueDate, r.ReminderDate, r.OpportunityId);
        if (result.IsFailure) return result;

        if (r.AssignedUserId.HasValue && r.AssignedUserId.Value != a.AssignedUserId)
        {
            var m = await _members.GetMemberAsync(r.AssignedUserId.Value, ct);
            if (m.IsFailure) return m;
            var re = a.UpdateAssignee(m.Value.Id, m.Value.DisplayName);
            if (re.IsFailure) return re;
        }

        await _repo.UpdateAsync(a, ct);
        return Result.Success();
    }
}

public sealed record CompleteActivityCommand(Guid Id) : IRequest<Result>;

public sealed class CompleteActivityCommandHandler : IRequestHandler<CompleteActivityCommand, Result>
{
    private readonly ISalesActivityRepository _repo;
    public CompleteActivityCommandHandler(ISalesActivityRepository repo) => _repo = repo;

    public async Task<Result> Handle(CompleteActivityCommand cmd, CancellationToken ct)
    {
        var a = await _repo.GetByIdAsync(cmd.Id, ct);
        if (a is null) return Result.Failure(Error.Validation("Id", "Activité introuvable"));
        a.Complete();
        await _repo.UpdateAsync(a, ct);
        return Result.Success();
    }
}

public sealed record DeleteActivityCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteActivityCommandHandler : IRequestHandler<DeleteActivityCommand, Result>
{
    private readonly ISalesActivityRepository _repo;
    public DeleteActivityCommandHandler(ISalesActivityRepository repo) => _repo = repo;

    public async Task<Result> Handle(DeleteActivityCommand cmd, CancellationToken ct)
    {
        var a = await _repo.GetByIdAsync(cmd.Id, ct);
        if (a is null) return Result.Failure(Error.Validation("Id", "Activité introuvable"));
        await _repo.DeleteAsync(a, ct);
        return Result.Success();
    }
}

// ==================== SALES TARGETS ====================

public sealed record GetSalesTargetsQuery(int Year, Guid? UserId, int? Month) : IRequest<Result<IReadOnlyList<SalesTargetDto>>>;

public sealed class GetSalesTargetsQueryHandler : IRequestHandler<GetSalesTargetsQuery, Result<IReadOnlyList<SalesTargetDto>>>
{
    private readonly ISalesTargetRepository _repo;
    private readonly IInvoiceRepository _invoices;

    public GetSalesTargetsQueryHandler(ISalesTargetRepository repo, IInvoiceRepository invoices)
    {
        _repo = repo;
        _invoices = invoices;
    }

    public async Task<Result<IReadOnlyList<SalesTargetDto>>> Handle(GetSalesTargetsQuery request, CancellationToken ct)
    {
        var items = (await _repo.GetByYearAsync(request.Year, request.UserId, ct)).ToList();
        if (request.Month is >= 1 and <= 12)
            items = items.Where(t => t.Month == request.Month!.Value).ToList();

        IReadOnlyDictionary<(Guid UserId, int Month), decimal> achieved =
            new Dictionary<(Guid, int), decimal>();
        if (items.Count > 0)
            achieved = await _invoices.GetAchievedRevenueTndByUserMonthForYearAsync(request.Year, ct);

        var dtos = items.Select(t =>
        {
            var a = achieved.GetValueOrDefault((t.UserId, t.Month), 0m);
            var target = t.TargetAmount.Amount;
            var pct = target > 0 ? Math.Min(100m, Math.Round(a / target * 100m, 2)) : 0m;
            return new SalesTargetDto
            {
                Id = t.Id,
                UserId = t.UserId,
                UserName = t.UserName,
                Year = t.Year,
                Month = t.Month,
                TargetAmount = target,
                AchievedAmount = a,
                ProgressPercent = pct,
                Currency = t.TargetAmount.Currency
            };
        }).ToList();
        return Result.Success<IReadOnlyList<SalesTargetDto>>(dtos);
    }
}

public sealed record CreateSalesTargetCommand(CreateSalesTargetRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateSalesTargetCommandHandler : IRequestHandler<CreateSalesTargetCommand, Result<Guid>>
{
    private readonly ISalesTargetRepository _repo;
    public CreateSalesTargetCommandHandler(ISalesTargetRepository repo) => _repo = repo;

    public async Task<Result<Guid>> Handle(CreateSalesTargetCommand cmd, CancellationToken ct)
    {
        var r = cmd.Request;
        var existing = await _repo.GetByUserYearMonthAsync(r.UserId, r.Year, r.Month, ct);
        if (existing is not null)
            return Result.Failure<Guid>(Error.Conflict("Un objectif existe déjà pour ce commercial/mois."));
        var create = SalesTarget.Create(r.UserId, r.UserName, r.Year, r.Month, Money.Create(r.TargetAmount));
        if (create.IsFailure) return Result.Failure<Guid>(create.Error);
        var entity = create.Value;
        entity.SetAuditInfo("system", false);
        await _repo.AddAsync(entity, ct);
        return Result.Success(entity.Id);
    }
}

public sealed record UpdateSalesTargetCommand(Guid Id, decimal TargetAmount) : IRequest<Result>;

public sealed class UpdateSalesTargetCommandHandler : IRequestHandler<UpdateSalesTargetCommand, Result>
{
    private readonly ISalesTargetRepository _repo;
    public UpdateSalesTargetCommandHandler(ISalesTargetRepository repo) => _repo = repo;

    public async Task<Result> Handle(UpdateSalesTargetCommand cmd, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(cmd.Id, ct);
        if (t is null) return Result.Failure(Error.Validation("Id", "Objectif introuvable"));
        var result = t.Update(Money.Create(cmd.TargetAmount));
        if (result.IsFailure) return result;
        await _repo.UpdateAsync(t, ct);
        return Result.Success();
    }
}

public sealed record DeleteSalesTargetCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteSalesTargetCommandHandler : IRequestHandler<DeleteSalesTargetCommand, Result>
{
    private readonly ISalesTargetRepository _repo;
    public DeleteSalesTargetCommandHandler(ISalesTargetRepository repo) => _repo = repo;

    public async Task<Result> Handle(DeleteSalesTargetCommand cmd, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(cmd.Id, ct);
        if (t is null) return Result.Failure(Error.Validation("Id", "Objectif introuvable"));
        await _repo.DeleteAsync(t, ct);
        return Result.Success();
    }
}

// ==================== QUOTE TEMPLATES ====================

public sealed record GetQuoteTemplatesQuery(bool? ActiveOnly, string? Search = null) : IRequest<Result<IReadOnlyList<QuoteTemplateDto>>>;

public sealed class GetQuoteTemplatesQueryHandler : IRequestHandler<GetQuoteTemplatesQuery, Result<IReadOnlyList<QuoteTemplateDto>>>
{
    private readonly IQuoteTemplateRepository _repo;
    private readonly IProductRepository _productRepository;

    public GetQuoteTemplatesQueryHandler(IQuoteTemplateRepository repo, IProductRepository productRepository)
    {
        _repo = repo;
        _productRepository = productRepository;
    }

    public async Task<Result<IReadOnlyList<QuoteTemplateDto>>> Handle(GetQuoteTemplatesQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.ActiveOnly, request.Search, ct);
        var allProductIds = items.SelectMany(t => t.Lines).Select(l => l.ProductId).Distinct().ToList();
        var names = await _productRepository.GetProductNamesByIdsAsync(allProductIds, ct);

        var dtos = items.Select(t => MapQuoteTemplate(t, names)).ToList();
        return Result.Success<IReadOnlyList<QuoteTemplateDto>>(dtos);
    }

    internal static QuoteTemplateDto MapQuoteTemplate(QuoteTemplate t, IReadOnlyDictionary<Guid, string> productNames) =>
        new()
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            DefaultNotes = t.DefaultNotes,
            DefaultTermsAndConditions = t.DefaultTermsAndConditions,
            DefaultValidityDays = t.DefaultValidityDays,
            IsActive = t.IsActive,
            UsageCount = t.UsageCount,
            Lines = t.Lines.OrderBy(l => l.SortOrder).Select(l => new QuoteTemplateLineDto
            {
                Id = l.Id,
                ProductId = l.ProductId,
                ProductName = productNames.TryGetValue(l.ProductId, out var pn) ? pn : null,
                Quantity = l.Quantity,
                CustomUnitPrice = l.CustomUnitPrice?.Amount,
                DiscountPercent = l.DiscountPercent,
                SortOrder = l.SortOrder
            }).ToList()
        };
}

public sealed record GetQuoteTemplateByIdQuery(Guid Id) : IRequest<Result<QuoteTemplateDto>>;

public sealed class GetQuoteTemplateByIdQueryHandler : IRequestHandler<GetQuoteTemplateByIdQuery, Result<QuoteTemplateDto>>
{
    private readonly IQuoteTemplateRepository _repo;
    private readonly IProductRepository _productRepository;

    public GetQuoteTemplateByIdQueryHandler(IQuoteTemplateRepository repo, IProductRepository productRepository)
    {
        _repo = repo;
        _productRepository = productRepository;
    }

    public async Task<Result<QuoteTemplateDto>> Handle(GetQuoteTemplateByIdQuery request, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(request.Id, ct);
        if (t is null)
            return Result.Failure<QuoteTemplateDto>(Error.Validation("Id", "Modèle introuvable"));

        var ids = t.Lines.Select(l => l.ProductId).Distinct().ToList();
        var names = await _productRepository.GetProductNamesByIdsAsync(ids, ct);
        return Result.Success(GetQuoteTemplatesQueryHandler.MapQuoteTemplate(t, names));
    }
}

public sealed record CreateQuoteTemplateCommand(CreateQuoteTemplateRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateQuoteTemplateCommandHandler : IRequestHandler<CreateQuoteTemplateCommand, Result<Guid>>
{
    private readonly IQuoteTemplateRepository _repo;
    public CreateQuoteTemplateCommandHandler(IQuoteTemplateRepository repo) => _repo = repo;

    public async Task<Result<Guid>> Handle(CreateQuoteTemplateCommand cmd, CancellationToken ct)
    {
        var r = cmd.Request;
        var create = QuoteTemplate.Create(r.Name, r.Description, r.DefaultNotes, r.DefaultTermsAndConditions, r.DefaultValidityDays);
        if (create.IsFailure) return Result.Failure<Guid>(create.Error);
        var entity = create.Value;
        int sort = 0;
        foreach (var line in r.Lines)
        {
            var tl = QuoteTemplateLine.Create(entity, line.ProductId, line.Quantity, line.SortOrder > 0 ? line.SortOrder : ++sort,
                line.CustomUnitPrice.HasValue ? Money.Create(line.CustomUnitPrice.Value) : null, line.DiscountPercent);
            entity.AddLine(tl);
        }
        entity.SetAuditInfo("system", false);
        await _repo.AddAsync(entity, ct);
        return Result.Success(entity.Id);
    }
}

public sealed record UpdateQuoteTemplateCommand(Guid Id, UpdateQuoteTemplateRequest Request, string UpdatedBy) : IRequest<Result>;

public sealed class UpdateQuoteTemplateCommandHandler : IRequestHandler<UpdateQuoteTemplateCommand, Result>
{
    private readonly IQuoteTemplateRepository _repo;
    public UpdateQuoteTemplateCommandHandler(IQuoteTemplateRepository repo) => _repo = repo;

    public async Task<Result> Handle(UpdateQuoteTemplateCommand cmd, CancellationToken ct)
    {
        return await _repo.UpdateContentAsync(cmd.Id, cmd.Request, cmd.UpdatedBy, ct);
    }
}

public sealed record SetQuoteTemplateActiveCommand(Guid Id, bool IsActive, string UpdatedBy) : IRequest<Result>;

public sealed class SetQuoteTemplateActiveCommandHandler : IRequestHandler<SetQuoteTemplateActiveCommand, Result>
{
    private readonly IQuoteTemplateRepository _repo;
    public SetQuoteTemplateActiveCommandHandler(IQuoteTemplateRepository repo) => _repo = repo;

    public async Task<Result> Handle(SetQuoteTemplateActiveCommand cmd, CancellationToken ct)
    {
        return await _repo.SetActiveAsync(cmd.Id, cmd.IsActive, cmd.UpdatedBy, ct);
    }
}

public sealed record DeleteQuoteTemplateCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteQuoteTemplateCommandHandler : IRequestHandler<DeleteQuoteTemplateCommand, Result>
{
    private readonly IQuoteTemplateRepository _repo;
    public DeleteQuoteTemplateCommandHandler(IQuoteTemplateRepository repo) => _repo = repo;

    public async Task<Result> Handle(DeleteQuoteTemplateCommand cmd, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(cmd.Id, ct);
        if (t is null) return Result.Failure(Error.Validation("Id", "Modèle introuvable"));
        await _repo.DeleteAsync(t, ct);
        return Result.Success();
    }
}
