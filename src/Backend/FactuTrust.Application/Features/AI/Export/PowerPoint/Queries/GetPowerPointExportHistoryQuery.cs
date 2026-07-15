using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Queries;

/// <summary>
/// Paginated history of PowerPoint exports for a single user, ordered by recency. Used by the
/// "Historique d'exports" panel in the assistant UI.
/// </summary>
public sealed record GetPowerPointExportHistoryQuery(Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<PowerPointExportAuditDto>>>;

public sealed class GetPowerPointExportHistoryQueryHandler
    : IRequestHandler<GetPowerPointExportHistoryQuery, Result<PagedResult<PowerPointExportAuditDto>>>
{
    private readonly IAiExportAuditRepository _repository;

    public GetPowerPointExportHistoryQueryHandler(IAiExportAuditRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PagedResult<PowerPointExportAuditDto>>> Handle(
        GetPowerPointExportHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var paged = await _repository.GetByUserAsync(request.UserId, page, pageSize, cancellationToken);

        var dtos = paged.Items.Select(a => new PowerPointExportAuditDto
        {
            Id = a.Id,
            Format = a.Format,
            Template = a.Template,
            Title = a.Title,
            ResponseCount = a.ResponseCount,
            SlideCount = a.SlideCount,
            SizeBytes = a.SizeBytes,
            GeneratedAt = a.GeneratedAt,
            Success = a.Success
        }).ToList();

        return Result.Success(PagedResult<PowerPointExportAuditDto>.Create(
            dtos,
            paged.Page,
            paged.PageSize,
            paged.TotalCount));
    }
}
