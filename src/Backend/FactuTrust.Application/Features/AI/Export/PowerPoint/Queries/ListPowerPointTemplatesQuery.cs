using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Queries;

/// <summary>
/// Returns the catalogue of available PowerPoint templates with their visual metadata. Used by
/// the export dialog to render preview cards.
/// </summary>
public sealed record ListPowerPointTemplatesQuery : IRequest<Result<IReadOnlyList<PowerPointTemplateInfoDto>>>;

public sealed class ListPowerPointTemplatesQueryHandler
    : IRequestHandler<ListPowerPointTemplatesQuery, Result<IReadOnlyList<PowerPointTemplateInfoDto>>>
{
    private readonly IPowerPointTemplateCatalog _catalog;

    public ListPowerPointTemplatesQueryHandler(IPowerPointTemplateCatalog catalog)
    {
        _catalog = catalog;
    }

    public Task<Result<IReadOnlyList<PowerPointTemplateInfoDto>>> Handle(
        ListPowerPointTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var list = _catalog.List();
        return Task.FromResult(Result.Success(list));
    }
}
