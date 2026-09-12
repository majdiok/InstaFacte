using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class CurrencyRepository : ICurrencyRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CurrencyRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Currency>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.Currencies.AsNoTracking();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        // Devise de tenue en tête, puis ordre alphabétique.
        return await query
            .OrderByDescending(c => c.IsFunctional)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<Currency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Currencies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        await using var context = _contextFactory.CreateContext();
        return await context.Currencies.FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }

    public async Task<Currency?> GetFunctionalAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.IsFunctional, cancellationToken);
    }

    public async Task AddAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Currencies.Add(currency);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Currency currency, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Currencies.Update(currency);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CurrencyExchangeRate>> GetRatesAsync(Guid currencyId, int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CurrencyExchangeRates
            .AsNoTracking()
            .Where(r => r.CurrencyId == currencyId && r.FiscalYear == fiscalYear)
            .OrderBy(r => r.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountRatesByCurrencyAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var counts = await context.CurrencyExchangeRates
            .AsNoTracking()
            .Where(r => r.FiscalYear == fiscalYear)
            .GroupBy(r => r.CurrencyId)
            .Select(g => new { CurrencyId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.CurrencyId, x => x.Count);
    }

    public async Task ReplaceRatesAsync(Guid currencyId, int fiscalYear, IReadOnlyList<CurrencyExchangeRate> rates, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var existing = await context.CurrencyExchangeRates
            .Where(r => r.CurrencyId == currencyId && r.FiscalYear == fiscalYear)
            .ToListAsync(cancellationToken);

        context.CurrencyExchangeRates.RemoveRange(existing);
        if (rates.Count > 0)
            context.CurrencyExchangeRates.AddRange(rates);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsUsedByJournalEntriesAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntries.AnyAsync(e => e.CurrencyCode == normalized, cancellationToken);
    }
}
