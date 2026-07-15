using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Résout la clé de modèle effective : surcharge ponctuelle valide > préférence du tenant > défaut.
/// </summary>
public sealed class DocumentTemplateResolver : IDocumentTemplateResolver
{
    private readonly IDocumentTemplatePreferenceRepository _repository;
    private readonly IDocumentTemplateRegistry _registry;
    private readonly ITenantContext _tenantContext;

    public DocumentTemplateResolver(
        IDocumentTemplatePreferenceRepository repository,
        IDocumentTemplateRegistry registry,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _registry = registry;
        _tenantContext = tenantContext;
    }

    public async Task<string> ResolveAsync(
        PrintableDocumentType documentType,
        string? overrideKey,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(overrideKey) && _registry.Exists(overrideKey))
            return overrideKey;

        var tenantId = _tenantContext.TenantId;
        if (tenantId is { } tid && tid != Guid.Empty)
        {
            var preference = await _repository.GetByTypeAsync(tid, documentType, cancellationToken);
            if (preference is not null && _registry.Exists(preference.TemplateKey))
                return preference.TemplateKey;
        }

        return _registry.DefaultKey;
    }
}
