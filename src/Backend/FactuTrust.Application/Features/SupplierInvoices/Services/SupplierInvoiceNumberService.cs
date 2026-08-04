using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;

namespace FactuTrust.Application.Features.SupplierInvoices.Services;

public sealed class SupplierInvoiceNumberService : ISupplierInvoiceNumberService
{
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly ITenantContext _tenantContext;

    public SupplierInvoiceNumberService(
        IDocumentNumberService documentNumberService,
        ISupplierInvoiceRepository supplierInvoiceRepository,
        ITenantContext tenantContext)
    {
        _documentNumberService = documentNumberService;
        _supplierInvoiceRepository = supplierInvoiceRepository;
        _tenantContext = tenantContext;
    }

    public async Task<string> PreviewNextAsync(DateTime invoiceDate, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var fiscalYear = invoiceDate.Year;
        return await _documentNumberService.PreviewNextAsync(
            tenantId,
            NumberingDocumentType.SupplierInvoice,
            fiscalYear,
            invoiceDate,
            cancellationToken);
    }

    public async Task<string> ReserveNextAsync(DateTime invoiceDate, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var fiscalYear = invoiceDate.Year;
        var result = await _documentNumberService.ReserveNextAsync(
            tenantId,
            NumberingDocumentType.SupplierInvoice,
            fiscalYear,
            invoiceDate,
            cancellationToken);
        return result.Value;
    }

    public async Task<bool> IsAvailableAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return false;

        return !await _supplierInvoiceRepository.ExistsByInvoiceNumberAsync(invoiceNumber.Trim(), cancellationToken);
    }

    private Guid RequireTenantId()
    {
        if (_tenantContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context required.");

        return tenantId;
    }
}
