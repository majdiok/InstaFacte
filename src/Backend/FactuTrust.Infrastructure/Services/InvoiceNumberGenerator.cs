using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Thread-safe invoice number generator delegating to unified document numbering.
/// </summary>
public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;
    private readonly ILogger<InvoiceNumberGenerator> _logger;

    public InvoiceNumberGenerator(
        IDocumentNumberService documentNumberService,
        IDbContextFactory<TenantDbContext> contextFactory,
        ILogger<InvoiceNumberGenerator> logger)
    {
        _documentNumberService = documentNumberService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<InvoiceNumber> ReserveNextNumberAsync(
        Guid tenantId,
        string prefix,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var docType = DocumentNumberMapper.ToNumberingType(prefix);
        var result = await _documentNumberService.ReserveNextAsync(
            tenantId, docType, fiscalYear, new DateTime(fiscalYear, 1, 1), cancellationToken);

        _logger.LogInformation(
            "Reserved invoice number {Number} for Tenant {TenantId}",
            result.Value, tenantId);

        return DocumentNumberMapper.ToInvoiceNumber(result);
    }

    public Task<string> PreviewNextNumberAsync(
        Guid tenantId,
        string prefix,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var docType = DocumentNumberMapper.ToNumberingType(prefix);
        return _documentNumberService.PreviewNextAsync(
            tenantId, docType, fiscalYear, new DateTime(fiscalYear, 1, 1), cancellationToken);
    }

    public async Task<bool> ValidateSequenceIntegrityAsync(
        Guid tenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var invoiceNumbers = await context.Invoices
            .AsNoTracking()
            .Where(i => i.Number.Year == fiscalYear)
            .Select(i => i.Number.Sequence)
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);

        if (invoiceNumbers.Count == 0)
            return true;

        for (var i = 0; i < invoiceNumbers.Count; i++)
        {
            var expected = i + 1;
            if (invoiceNumbers[i] != expected)
            {
                _logger.LogWarning(
                    "Sequence integrity violation: Expected {Expected}, found {Found} for Tenant {TenantId}, Year {Year}",
                    expected, invoiceNumbers[i], tenantId, fiscalYear);
                return false;
            }
        }

        return true;
    }
}
