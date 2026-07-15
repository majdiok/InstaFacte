using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Thread-safe quote number generator delegating to unified document numbering.
/// </summary>
public sealed class QuoteNumberGenerator : IQuoteNumberGenerator
{
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;
    private readonly ILogger<QuoteNumberGenerator> _logger;

    public QuoteNumberGenerator(
        IDocumentNumberService documentNumberService,
        IDbContextFactory<TenantDbContext> contextFactory,
        ILogger<QuoteNumberGenerator> logger)
    {
        _documentNumberService = documentNumberService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<QuoteNumber> ReserveNextNumberAsync(
        Guid tenantId,
        string prefix,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var result = await _documentNumberService.ReserveNextAsync(
            tenantId,
            DocumentNumberMapper.ToNumberingType(prefix),
            fiscalYear,
            new DateTime(fiscalYear, 1, 1),
            cancellationToken);

        _logger.LogInformation(
            "Reserved quote number {Number} for Tenant {TenantId}",
            result.Value, tenantId);

        return DocumentNumberMapper.ToQuoteNumber(result);
    }

    public Task<string> PreviewNextNumberAsync(
        Guid tenantId,
        string prefix,
        int fiscalYear,
        CancellationToken cancellationToken = default) =>
        _documentNumberService.PreviewNextAsync(
            tenantId,
            DocumentNumberMapper.ToNumberingType(prefix),
            fiscalYear,
            new DateTime(fiscalYear, 1, 1),
            cancellationToken);

    public async Task<bool> ValidateSequenceIntegrityAsync(
        Guid tenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var quoteNumbers = await context.Quotes
            .AsNoTracking()
            .Where(q => q.Number.Year == fiscalYear)
            .Select(q => q.Number.Sequence)
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);

        if (quoteNumbers.Count == 0)
            return true;

        for (var i = 0; i < quoteNumbers.Count; i++)
        {
            if (quoteNumbers[i] != i + 1)
                return false;
        }

        return true;
    }
}
