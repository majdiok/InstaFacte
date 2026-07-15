using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using MediatR;

namespace FactuTrust.Application.Features.Numbering.Queries;

public sealed record GetNumberingSchemesQuery(int? FiscalYear = null) : IRequest<IReadOnlyList<NumberingSchemeDto>>;

public sealed class GetNumberingSchemesQueryHandler : IRequestHandler<GetNumberingSchemesQuery, IReadOnlyList<NumberingSchemeDto>>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly IDocumentNumberService _numberService;
    private readonly ITenantContext _tenantContext;

    public GetNumberingSchemesQueryHandler(INumberingSchemeRepository repository, IDocumentNumberService numberService, ITenantContext tenantContext)
    {
        _repository = repository;
        _numberService = numberService;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<NumberingSchemeDto>> Handle(GetNumberingSchemesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context required.");
        var fiscalYear = request.FiscalYear ?? DateTime.UtcNow.Year;
        var existing = await _repository.GetAllForYearAsync(tenantId, fiscalYear, cancellationToken);
        var existingByType = existing.ToDictionary(s => s.DocumentType);
        var result = new List<NumberingSchemeDto>();

        foreach (NumberingDocumentType docType in Enum.GetValues<NumberingDocumentType>())
        {
            var effectiveSequence = await _numberService.GetEffectiveCurrentSequenceAsync(
                tenantId, docType, fiscalYear, cancellationToken);
            var preview = await _numberService.PreviewNextAsync(
                tenantId, docType, fiscalYear, DateTime.UtcNow, cancellationToken);

            result.Add(existingByType.TryGetValue(docType, out var scheme)
                ? NumberingMappings.ToDto(scheme, preview, effectiveSequence)
                : NumberingMappings.ToDefaultDto(docType, fiscalYear, preview, effectiveSequence));
        }

        return result;
    }
}

public sealed record GetNumberingSchemeByTypeQuery(NumberingDocumentType DocumentType, int? FiscalYear = null) : IRequest<NumberingSchemeDto>;

public sealed class GetNumberingSchemeByTypeQueryHandler : IRequestHandler<GetNumberingSchemeByTypeQuery, NumberingSchemeDto>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly IDocumentNumberService _numberService;
    private readonly ITenantContext _tenantContext;

    public GetNumberingSchemeByTypeQueryHandler(INumberingSchemeRepository repository, IDocumentNumberService numberService, ITenantContext tenantContext)
    {
        _repository = repository;
        _numberService = numberService;
        _tenantContext = tenantContext;
    }

    public async Task<NumberingSchemeDto> Handle(GetNumberingSchemeByTypeQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context required.");
        var fiscalYear = request.FiscalYear ?? DateTime.UtcNow.Year;
        var effectiveSequence = await _numberService.GetEffectiveCurrentSequenceAsync(
            tenantId, request.DocumentType, fiscalYear, cancellationToken);
        var scheme = await _repository.GetByTypeAsync(tenantId, request.DocumentType, fiscalYear, cancellationToken);
        var preview = await _numberService.PreviewNextAsync(
            tenantId, request.DocumentType, fiscalYear, DateTime.UtcNow, cancellationToken);

        return scheme is not null
            ? NumberingMappings.ToDto(scheme, preview, effectiveSequence)
            : NumberingMappings.ToDefaultDto(request.DocumentType, fiscalYear, preview, effectiveSequence);
    }
}

public sealed record PreviewNumberingQuery(NumberingDocumentType DocumentType, PreviewNumberingRequest Request) : IRequest<PreviewNumberingResponse>;

public sealed class PreviewNumberingQueryHandler : IRequestHandler<PreviewNumberingQuery, PreviewNumberingResponse>
{
    public Task<PreviewNumberingResponse> Handle(PreviewNumberingQuery request, CancellationToken cancellationToken)
    {
        var blocks = NumberingMappings.ToDomainBlocks(request.Request.Blocks);
        var validation = NumberingFormatRenderer.ValidateBlocks(blocks);
        if (validation.IsFailure) throw new InvalidOperationException(validation.Error.Description);

        var fiscalYear = request.Request.FiscalYear ?? DateTime.UtcNow.Year;
        var referenceDate = new DateTime(fiscalYear, DateTime.UtcNow.Month, DateTime.UtcNow.Day);
        var sequence = Math.Max(1, request.Request.StartNumber);
        var freeText = blocks.FirstOrDefault(b => b.Type == NumberingBlockType.FreeText)?.Value ?? request.DocumentType.DefaultFreeText();
        var render = NumberingFormatRenderer.Render(blocks, new NumberingFormatRenderer.RenderContext(sequence, referenceDate, freeText));
        if (render.IsFailure) throw new InvalidOperationException(render.Error.Description);
        return Task.FromResult(new PreviewNumberingResponse { Example = render.Value });
    }
}
