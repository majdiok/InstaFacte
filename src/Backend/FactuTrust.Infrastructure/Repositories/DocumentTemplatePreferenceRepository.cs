using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class DocumentTemplatePreferenceRepository : IDocumentTemplatePreferenceRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public DocumentTemplatePreferenceRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<DocumentTemplatePreference>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.DocumentTemplatePreferences
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.DocumentType)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentTemplatePreference?> GetByTypeAsync(Guid tenantId, PrintableDocumentType documentType, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.DocumentTemplatePreferences
            .Where(p => p.TenantId == tenantId && p.DocumentType == documentType)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(DocumentTemplatePreference preference, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.DocumentTemplatePreferences.Add(preference);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DocumentTemplatePreference preference, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.DocumentTemplatePreferences.Update(preference);
        await context.SaveChangesAsync(cancellationToken);
    }
}
