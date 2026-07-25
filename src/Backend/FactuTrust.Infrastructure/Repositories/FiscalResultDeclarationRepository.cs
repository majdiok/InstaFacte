using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class FiscalResultDeclarationRepository : IFiscalResultDeclarationRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public FiscalResultDeclarationRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<FiscalResultDeclaration?> GetByYearAsync(int fiscalYear, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.FiscalResultDeclarations
            .AsNoTracking()
            .Include(d => d.Adjustments)
            .Include(d => d.CarryForwards)
            .FirstOrDefaultAsync(d => d.FiscalYear == fiscalYear, ct);
    }

    public async Task<FiscalResultDeclaration> UpsertAsync(FiscalDeclarationUpsert u, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();

        var decl = await context.FiscalResultDeclarations
            .Include(d => d.Adjustments)
            .Include(d => d.CarryForwards)
            .FirstOrDefaultAsync(d => d.FiscalYear == u.FiscalYear, ct);

        if (decl is null)
        {
            decl = FiscalResultDeclaration.Create(u.FiscalYear, u.TaxpayerKind);
            context.FiscalResultDeclarations.Add(decl);
        }

        decl.UpdateInputs(
            u.TaxpayerKind,
            u.AccountingResult,
            u.AppliedIsRate,
            u.LocalTurnoverTtc,
            u.AcomptesPaid,
            u.WithholdingSuffered,
            u.PriorTaxCredit,
            u.MinimumTaxRegime);

        // Le parent étant suivi avec ses enfants (Include), le remplacement des collections déclenche
        // la suppression des lignes retirées (cascade) et l'insertion des nouvelles.
        decl.ReplaceAdjustments(u.Adjustments);
        decl.ReplaceCarryForwards(u.CarryForwards);

        await context.SaveChangesAsync(ct);
        return decl;
    }

    public async Task<bool> FinalizeAsync(int fiscalYear, string userId, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        var decl = await context.FiscalResultDeclarations
            .FirstOrDefaultAsync(d => d.FiscalYear == fiscalYear, ct);
        if (decl is null)
            return false;

        decl.Finalize(userId);
        await context.SaveChangesAsync(ct);
        return true;
    }
}
