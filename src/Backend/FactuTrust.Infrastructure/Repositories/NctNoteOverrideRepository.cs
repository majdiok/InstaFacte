using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class NctNoteOverrideRepository : INctNoteOverrideRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public NctNoteOverrideRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<NctNoteOverride>> GetByYearAsync(int fiscalYear, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.NctNoteOverrides
            .AsNoTracking()
            .Where(o => o.FiscalYear == fiscalYear)
            .OrderBy(o => o.NoteNumber)
            .ToListAsync(ct);
    }

    public async Task<Result<NctNoteOverride>> UpsertAsync(
        int fiscalYear, int noteNumber, string? customTitle, string? customDescription, bool isHidden,
        CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();

        var row = await context.NctNoteOverrides
            .FirstOrDefaultAsync(o => o.FiscalYear == fiscalYear && o.NoteNumber == noteNumber, ct);

        if (row is null)
        {
            var created = NctNoteOverride.Create(fiscalYear, noteNumber, customTitle, customDescription, isHidden);
            if (created.IsFailure)
                return Result.Failure<NctNoteOverride>(created.Error);

            row = created.Value;
            row.SetAuditInfo("system", isUpdate: false);
            context.NctNoteOverrides.Add(row);
        }
        else
        {
            var updated = row.Update(customTitle, customDescription, isHidden);
            if (updated.IsFailure)
                return Result.Failure<NctNoteOverride>(updated.Error);

            row.SetAuditInfo("system", isUpdate: true);
        }

        await context.SaveChangesAsync(ct);
        return Result.Success(row);
    }

    public async Task DeleteAsync(int fiscalYear, int noteNumber, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();

        var row = await context.NctNoteOverrides
            .FirstOrDefaultAsync(o => o.FiscalYear == fiscalYear && o.NoteNumber == noteNumber, ct);
        if (row is null)
            return;   // « Rétablir » sur une note jamais personnalisée : sans effet, pas une erreur.

        context.NctNoteOverrides.Remove(row);
        await context.SaveChangesAsync(ct);
    }
}
