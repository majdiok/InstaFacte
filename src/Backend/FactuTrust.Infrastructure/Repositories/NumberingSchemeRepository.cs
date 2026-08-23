using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class NumberingSchemeRepository : INumberingSchemeRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public NumberingSchemeRepository(ITenantDbContextFactory contextFactory) => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<DocumentNumberingScheme>> GetAllForYearAsync(Guid tenantId, int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.DocumentNumberingSchemes.AsNoTracking().Where(s => s.TenantId == tenantId && s.FiscalYear == fiscalYear).OrderBy(s => s.DocumentType).ToListAsync(cancellationToken);
    }

    public async Task<DocumentNumberingScheme?> GetByTypeAsync(Guid tenantId, NumberingDocumentType documentType, int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.DocumentNumberingSchemes.Where(s => s.TenantId == tenantId && s.DocumentType == documentType && s.FiscalYear == fiscalYear).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(DocumentNumberingScheme scheme, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.DocumentNumberingSchemes.Add(scheme);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DocumentNumberingScheme scheme, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.DocumentNumberingSchemes.Update(scheme);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasDocumentsAsync(Guid tenantId, NumberingDocumentType documentType, int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return documentType switch
        {
            NumberingDocumentType.Invoice => await context.Invoices.AnyAsync(i => i.Number.Year == fiscalYear && i.Number.Prefix == "FAC" && i.Type == Domain.Enums.InvoiceType.Standard, cancellationToken),
            NumberingDocumentType.CreditNote => await context.Invoices.AnyAsync(i => i.Number.Year == fiscalYear && i.Number.Prefix == "AVO" && i.Type == Domain.Enums.InvoiceType.CreditNote, cancellationToken),
            NumberingDocumentType.Quote => await context.Quotes.AnyAsync(q => q.Number.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.DeliveryNote => await context.DeliveryNotes.AnyAsync(d => d.Number.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.PurchaseOrder => await context.PurchaseOrders.AnyAsync(p => p.Number.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.StockTransfer => await context.StockTransfers.AnyAsync(t => t.Number.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.PhysicalInventory => await context.PhysicalInventories.AnyAsync(i => i.StartedAt.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.CashReceipt => await context.CashOperations.AnyAsync(c => c.Number.Year == fiscalYear && c.Number.PrefixValue == "ENC", cancellationToken),
            NumberingDocumentType.CashExpense => await context.CashOperations.AnyAsync(c => c.Number.Year == fiscalYear && c.Number.PrefixValue == "DEP", cancellationToken),
            NumberingDocumentType.BankDeposit => await context.BankDeposits.AnyAsync(b => b.Number.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.SalesReturnNote => await context.SalesReturnNotes.AnyAsync(n => n.Number.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.ZReport => await context.ZReports.AnyAsync(z => z.Number.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.StockEntry => await context.StockVouchers.AnyAsync(
                v => v.Kind == StockVoucherKind.Entry && v.Number.Year == fiscalYear, cancellationToken),
            NumberingDocumentType.StockIssue => await context.StockVouchers.AnyAsync(
                v => v.Kind == StockVoucherKind.Issue && v.Number.Year == fiscalYear, cancellationToken),
            _ => false
        };
    }
}