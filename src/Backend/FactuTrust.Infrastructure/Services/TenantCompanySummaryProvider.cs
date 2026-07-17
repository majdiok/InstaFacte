using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class TenantCompanySummaryProvider : ITenantCompanySummaryProvider
{
    private readonly ITenantContext _tenantContext;
    private readonly MasterDbContext _masterContext;
    private readonly ICompanyRepository _companyRepository;

    public TenantCompanySummaryProvider(
        ITenantContext tenantContext,
        MasterDbContext masterContext,
        ICompanyRepository companyRepository)
    {
        _tenantContext = tenantContext;
        _masterContext = masterContext;
        _companyRepository = companyRepository;
    }

    public async Task<TenantCompanySummaryDto?> GetCurrentTenantSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue)
            return null;

        var tenant = await _masterContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId.Value, cancellationToken);
        if (tenant is null)
            return null;

        string? tradeName = null;
        try
        {
            var company = await _companyRepository.GetDefaultAsync(cancellationToken);
            tradeName = company?.TradeName;
        }
        catch
        {
            // Trade name is optional; tenant master data is sufficient.
        }

        return new TenantCompanySummaryDto
        {
            CompanyName = tenant.CompanyName,
            Nif = tenant.NIF.Value,
            TaxRegimeDisplay = tenant.TaxRegime.ToDisplayString(),
            TradeName = tradeName,
            AddressLine = tenant.Address.ToSingleLine()
        };
    }
}
