using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.DocumentTemplates.Queries;

internal static class DocumentTemplateDtoFactory
{
    public static DocumentTemplatePreferenceDto Build(
        PrintableDocumentType type, string? storedKey, IDocumentTemplateRegistry registry)
    {
        var key = !string.IsNullOrWhiteSpace(storedKey) && registry.Exists(storedKey!)
            ? storedKey!
            : registry.DefaultKey;
        var name = registry.Catalog.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase))?.Name ?? key;
        return new DocumentTemplatePreferenceDto(type.ToString(), (int)type, type.ToDisplayString(), key, name);
    }
}

// --- Liste de toutes les préférences ---

public sealed record GetDocumentTemplatesQuery : IRequest<IReadOnlyList<DocumentTemplatePreferenceDto>>;

public sealed class GetDocumentTemplatesQueryHandler
    : IRequestHandler<GetDocumentTemplatesQuery, IReadOnlyList<DocumentTemplatePreferenceDto>>
{
    private readonly IDocumentTemplatePreferenceRepository _repository;
    private readonly IDocumentTemplateRegistry _registry;
    private readonly ITenantContext _tenantContext;

    public GetDocumentTemplatesQueryHandler(
        IDocumentTemplatePreferenceRepository repository,
        IDocumentTemplateRegistry registry,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _registry = registry;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<DocumentTemplatePreferenceDto>> Handle(GetDocumentTemplatesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context required.");
        var existing = await _repository.GetAllAsync(tenantId, cancellationToken);
        var byType = existing.ToDictionary(p => p.DocumentType, p => p.TemplateKey);

        return Enum.GetValues<PrintableDocumentType>()
            .Select(t => DocumentTemplateDtoFactory.Build(t, byType.TryGetValue(t, out var k) ? k : null, _registry))
            .ToList();
    }
}

// --- Préférence d'un type donné ---

public sealed record GetDocumentTemplateByTypeQuery(PrintableDocumentType DocumentType) : IRequest<DocumentTemplatePreferenceDto>;

public sealed class GetDocumentTemplateByTypeQueryHandler
    : IRequestHandler<GetDocumentTemplateByTypeQuery, DocumentTemplatePreferenceDto>
{
    private readonly IDocumentTemplatePreferenceRepository _repository;
    private readonly IDocumentTemplateRegistry _registry;
    private readonly ITenantContext _tenantContext;

    public GetDocumentTemplateByTypeQueryHandler(
        IDocumentTemplatePreferenceRepository repository,
        IDocumentTemplateRegistry registry,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _registry = registry;
        _tenantContext = tenantContext;
    }

    public async Task<DocumentTemplatePreferenceDto> Handle(GetDocumentTemplateByTypeQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context required.");
        var pref = await _repository.GetByTypeAsync(tenantId, request.DocumentType, cancellationToken);
        return DocumentTemplateDtoFactory.Build(request.DocumentType, pref?.TemplateKey, _registry);
    }
}

// --- Catalogue des modèles disponibles ---

public sealed record GetDocumentTemplateCatalogQuery(PrintableDocumentType? DocumentType = null)
    : IRequest<IReadOnlyList<DocumentTemplateCatalogItemDto>>;

public sealed class GetDocumentTemplateCatalogQueryHandler
    : IRequestHandler<GetDocumentTemplateCatalogQuery, IReadOnlyList<DocumentTemplateCatalogItemDto>>
{
    private readonly IDocumentTemplateRegistry _registry;

    public GetDocumentTemplateCatalogQueryHandler(IDocumentTemplateRegistry registry) => _registry = registry;

    public Task<IReadOnlyList<DocumentTemplateCatalogItemDto>> Handle(GetDocumentTemplateCatalogQuery request, CancellationToken cancellationToken)
    {
        var items = request.DocumentType is { } type ? _registry.CatalogFor(type) : _registry.Catalog;
        IReadOnlyList<DocumentTemplateCatalogItemDto> result = items
            .Select(c => new DocumentTemplateCatalogItemDto(
                c.Key, c.Name, c.Description, c.ThumbnailAsset,
                c.SupportedDocumentTypes.Select(t => t.ToString()).ToList()))
            .ToList();
        return Task.FromResult(result);
    }
}

// --- Aperçu d'un modèle avec des données d'exemple ---

public sealed record PreviewDocumentTemplateQuery(PrintableDocumentType DocumentType, string? TemplateKey)
    : IRequest<byte[]>;

public sealed class PreviewDocumentTemplateQueryHandler : IRequestHandler<PreviewDocumentTemplateQuery, byte[]>
{
    private readonly IDocumentTemplateRegistry _registry;

    public PreviewDocumentTemplateQueryHandler(IDocumentTemplateRegistry registry) => _registry = registry;

    public Task<byte[]> Handle(PreviewDocumentTemplateQuery request, CancellationToken cancellationToken)
    {
        var model = SampleDocumentRenderModelFactory.Build(request.DocumentType);
        var bytes = _registry.Get(request.TemplateKey).Render(model);
        return Task.FromResult(bytes);
    }
}
