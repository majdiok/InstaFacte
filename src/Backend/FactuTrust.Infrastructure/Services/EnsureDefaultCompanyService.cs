using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Ensures the current tenant has a default Company in the tenant database.
/// If none exists, creates one from the Tenant (Master) data.
/// </summary>
public sealed class EnsureDefaultCompanyService : IEnsureDefaultCompanyService
{
    private readonly ICurrentUser _currentUser;
    private readonly MasterDbContext _masterContext;
    private readonly ICompanyRepository _companyRepository;

    public EnsureDefaultCompanyService(
        ICurrentUser currentUser,
        MasterDbContext masterContext,
        ICompanyRepository companyRepository)
    {
        _currentUser = currentUser;
        _masterContext = masterContext;
        _companyRepository = companyRepository;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> GetOrCreateDefaultCompanyIdAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized("Tenant context not found"));

        var tenant = await _masterContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

        if (tenant is null)
            return Result.Failure<Guid>(Error.NotFound("Tenant", tenantId.Value));

        var company = await _companyRepository.GetDefaultAsync(cancellationToken);
        if (company is not null)
            return Result.Success(company.Id);

        var companyResult = Company.Create(
            tenant.CompanyName,
            tenant.Address,
            tenant.NIF,
            tenant.Email,
            null,
            null,
            null,
            tenant.Phone,
            tenant.LogoUrl);

        if (companyResult.IsFailure)
            return Result.Failure<Guid>(companyResult.Error);

        company = companyResult.Value;
        company.SetAsDefault();
        company = await _companyRepository.AddAsync(company, cancellationToken);

        return Result.Success(company.Id);
    }
}
