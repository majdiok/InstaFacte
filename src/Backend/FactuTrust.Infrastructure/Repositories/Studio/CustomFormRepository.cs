using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories.Studio;

public sealed class CustomFormRepository : ICustomFormRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CustomFormRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<CustomFormDefinition?> GetDefaultByEntityAsync(Guid tenantId, Guid entityDefinitionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CustomFormDefinitions
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.EntityDefinitionId == entityDefinitionId && f.IsDefault, cancellationToken);
    }

    public async Task AddAsync(CustomFormDefinition form, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomFormDefinitions.Add(form);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomFormDefinition form, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CustomFormDefinitions.Update(form);
        await context.SaveChangesAsync(cancellationToken);
    }
}
