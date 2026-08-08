using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Audit;

public sealed record RunAccountingAuditCommand(AccountingAuditRunRequestDto Request)
    : IRequest<Result<AccountingAuditRunResultDto>>;

public sealed class RunAccountingAuditCommandHandler
    : IRequestHandler<RunAccountingAuditCommand, Result<AccountingAuditRunResultDto>>
{
    private readonly IAccountingAuditEngine _engine;
    private readonly ICurrentUser _currentUser;

    public RunAccountingAuditCommandHandler(IAccountingAuditEngine engine, ICurrentUser currentUser)
    {
        _engine = engine;
        _currentUser = currentUser;
    }

    public Task<Result<AccountingAuditRunResultDto>> Handle(
        RunAccountingAuditCommand request, CancellationToken cancellationToken) =>
        _engine.RunAsync(request.Request, _currentUser.UserId, _currentUser.Email, cancellationToken);
}

public sealed record GetAccountingAuditRunStatusQuery(Guid RunId)
    : IRequest<Result<AccountingAuditRunStatusDto>>;

public sealed class GetAccountingAuditRunStatusQueryHandler
    : IRequestHandler<GetAccountingAuditRunStatusQuery, Result<AccountingAuditRunStatusDto>>
{
    private readonly IAccountingAuditEngine _engine;
    public GetAccountingAuditRunStatusQueryHandler(IAccountingAuditEngine engine) => _engine = engine;
    public Task<Result<AccountingAuditRunStatusDto>> Handle(
        GetAccountingAuditRunStatusQuery request, CancellationToken cancellationToken) =>
        _engine.GetRunStatusAsync(request.RunId, cancellationToken);
}

public sealed record GetAccountingAuditDashboardQuery(int FiscalYear)
    : IRequest<Result<AccountingAuditDashboardDto>>;

public sealed class GetAccountingAuditDashboardQueryHandler
    : IRequestHandler<GetAccountingAuditDashboardQuery, Result<AccountingAuditDashboardDto>>
{
    private readonly IAccountingAuditQueryService _queries;
    public GetAccountingAuditDashboardQueryHandler(IAccountingAuditQueryService queries) => _queries = queries;
    public Task<Result<AccountingAuditDashboardDto>> Handle(
        GetAccountingAuditDashboardQuery request, CancellationToken cancellationToken) =>
        _queries.GetDashboardAsync(request.FiscalYear, cancellationToken);
}

public sealed record GetAccountingAuditAnomaliesQuery(AccountingAnomalyFilterDto Filter)
    : IRequest<Result<PagedAnomaliesDto>>;

public sealed class GetAccountingAuditAnomaliesQueryHandler
    : IRequestHandler<GetAccountingAuditAnomaliesQuery, Result<PagedAnomaliesDto>>
{
    private readonly IAccountingAuditQueryService _queries;
    public GetAccountingAuditAnomaliesQueryHandler(IAccountingAuditQueryService queries) => _queries = queries;
    public Task<Result<PagedAnomaliesDto>> Handle(
        GetAccountingAuditAnomaliesQuery request, CancellationToken cancellationToken) =>
        _queries.GetAnomaliesAsync(request.Filter, cancellationToken);
}

public sealed record GetAccountingAuditAnomalyDetailQuery(Guid AnomalyId)
    : IRequest<Result<AccountingAnomalyDetailDto>>;

public sealed class GetAccountingAuditAnomalyDetailQueryHandler
    : IRequestHandler<GetAccountingAuditAnomalyDetailQuery, Result<AccountingAnomalyDetailDto>>
{
    private readonly IAccountingAuditQueryService _queries;
    public GetAccountingAuditAnomalyDetailQueryHandler(IAccountingAuditQueryService queries) => _queries = queries;
    public Task<Result<AccountingAnomalyDetailDto>> Handle(
        GetAccountingAuditAnomalyDetailQuery request, CancellationToken cancellationToken) =>
        _queries.GetAnomalyDetailAsync(request.AnomalyId, cancellationToken);
}

public sealed record GetAccountingAuditAnalyticsQuery(int FiscalYear)
    : IRequest<Result<AccountingAuditAnalyticsDto>>;

public sealed class GetAccountingAuditAnalyticsQueryHandler
    : IRequestHandler<GetAccountingAuditAnalyticsQuery, Result<AccountingAuditAnalyticsDto>>
{
    private readonly IAccountingAuditQueryService _queries;
    public GetAccountingAuditAnalyticsQueryHandler(IAccountingAuditQueryService queries) => _queries = queries;
    public Task<Result<AccountingAuditAnalyticsDto>> Handle(
        GetAccountingAuditAnalyticsQuery request, CancellationToken cancellationToken) =>
        _queries.GetAnalyticsAsync(request.FiscalYear, cancellationToken);
}

public sealed record GetAccountingAuditModulesQuery(int FiscalYear)
    : IRequest<Result<IReadOnlyList<AccountingControlModuleDto>>>;

public sealed class GetAccountingAuditModulesQueryHandler
    : IRequestHandler<GetAccountingAuditModulesQuery, Result<IReadOnlyList<AccountingControlModuleDto>>>
{
    private readonly IAccountingAuditQueryService _queries;
    public GetAccountingAuditModulesQueryHandler(IAccountingAuditQueryService queries) => _queries = queries;
    public Task<Result<IReadOnlyList<AccountingControlModuleDto>>> Handle(
        GetAccountingAuditModulesQuery request, CancellationToken cancellationToken) =>
        _queries.GetModulesAsync(request.FiscalYear, cancellationToken);
}

public sealed record AssignAccountingAnomalyCommand(Guid AnomalyId, AssignAnomalyRequestDto Request)
    : IRequest<Result<AccountingAnomalyDetailDto>>;

public sealed class AssignAccountingAnomalyCommandHandler
    : IRequestHandler<AssignAccountingAnomalyCommand, Result<AccountingAnomalyDetailDto>>
{
    private readonly IAccountingAuditWorkflowService _workflow;
    private readonly ICurrentUser _currentUser;
    public AssignAccountingAnomalyCommandHandler(IAccountingAuditWorkflowService workflow, ICurrentUser currentUser)
    {
        _workflow = workflow;
        _currentUser = currentUser;
    }
    public Task<Result<AccountingAnomalyDetailDto>> Handle(
        AssignAccountingAnomalyCommand request, CancellationToken cancellationToken) =>
        _workflow.AssignAsync(request.AnomalyId, request.Request.AssigneeUserId, request.Request.AssigneeUserName,
            _currentUser.UserId ?? Guid.Empty, _currentUser.Email, cancellationToken);
}

public sealed record UpdateAccountingAnomalyStatusCommand(Guid AnomalyId, UpdateAnomalyStatusRequestDto Request)
    : IRequest<Result<AccountingAnomalyDetailDto>>;

public sealed class UpdateAccountingAnomalyStatusCommandHandler
    : IRequestHandler<UpdateAccountingAnomalyStatusCommand, Result<AccountingAnomalyDetailDto>>
{
    private readonly IAccountingAuditWorkflowService _workflow;
    private readonly ICurrentUser _currentUser;
    public UpdateAccountingAnomalyStatusCommandHandler(IAccountingAuditWorkflowService workflow, ICurrentUser currentUser)
    {
        _workflow = workflow;
        _currentUser = currentUser;
    }
    public Task<Result<AccountingAnomalyDetailDto>> Handle(
        UpdateAccountingAnomalyStatusCommand request, CancellationToken cancellationToken) =>
        _workflow.UpdateStatusAsync(request.AnomalyId, request.Request.Status,
            _currentUser.UserId ?? Guid.Empty, _currentUser.Email, cancellationToken);
}

public sealed record CommentAccountingAnomalyCommand(Guid AnomalyId, CommentAnomalyRequestDto Request)
    : IRequest<Result<AccountingAnomalyDetailDto>>;

public sealed class CommentAccountingAnomalyCommandHandler
    : IRequestHandler<CommentAccountingAnomalyCommand, Result<AccountingAnomalyDetailDto>>
{
    private readonly IAccountingAuditWorkflowService _workflow;
    private readonly ICurrentUser _currentUser;
    public CommentAccountingAnomalyCommandHandler(IAccountingAuditWorkflowService workflow, ICurrentUser currentUser)
    {
        _workflow = workflow;
        _currentUser = currentUser;
    }
    public Task<Result<AccountingAnomalyDetailDto>> Handle(
        CommentAccountingAnomalyCommand request, CancellationToken cancellationToken) =>
        _workflow.AddCommentAsync(request.AnomalyId, request.Request.Comment,
            _currentUser.UserId ?? Guid.Empty, _currentUser.Email, cancellationToken);
}

public sealed record IgnoreAccountingAnomalyCommand(Guid AnomalyId, IgnoreAnomalyRequestDto Request)
    : IRequest<Result<AccountingAnomalyDetailDto>>;

public sealed class IgnoreAccountingAnomalyCommandHandler
    : IRequestHandler<IgnoreAccountingAnomalyCommand, Result<AccountingAnomalyDetailDto>>
{
    private readonly IAccountingAuditWorkflowService _workflow;
    private readonly ICurrentUser _currentUser;
    public IgnoreAccountingAnomalyCommandHandler(IAccountingAuditWorkflowService workflow, ICurrentUser currentUser)
    {
        _workflow = workflow;
        _currentUser = currentUser;
    }
    public Task<Result<AccountingAnomalyDetailDto>> Handle(
        IgnoreAccountingAnomalyCommand request, CancellationToken cancellationToken) =>
        _workflow.IgnoreAsync(request.AnomalyId, request.Request.Reason,
            _currentUser.UserId ?? Guid.Empty, _currentUser.Email, cancellationToken);
}

public sealed record ResolveAccountingAnomalyCommand(Guid AnomalyId)
    : IRequest<Result<AccountingAnomalyDetailDto>>;

public sealed class ResolveAccountingAnomalyCommandHandler
    : IRequestHandler<ResolveAccountingAnomalyCommand, Result<AccountingAnomalyDetailDto>>
{
    private readonly IAccountingAuditWorkflowService _workflow;
    private readonly ICurrentUser _currentUser;
    public ResolveAccountingAnomalyCommandHandler(IAccountingAuditWorkflowService workflow, ICurrentUser currentUser)
    {
        _workflow = workflow;
        _currentUser = currentUser;
    }
    public Task<Result<AccountingAnomalyDetailDto>> Handle(
        ResolveAccountingAnomalyCommand request, CancellationToken cancellationToken) =>
        _workflow.ResolveAsync(request.AnomalyId, _currentUser.UserId ?? Guid.Empty, _currentUser.Email, cancellationToken);
}
