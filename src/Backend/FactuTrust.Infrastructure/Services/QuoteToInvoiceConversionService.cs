using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Performs atomic quote-to-invoice conversion in a single database transaction.
/// Prevents inconsistent state (invoice created but quote not marked converted).
/// </summary>
public sealed class QuoteToInvoiceConversionService : IQuoteToInvoiceConversionService
{
    private readonly TenantDbContextFactory _contextFactory;
    private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;
    private readonly IFiscalStampResolver _fiscalStampResolver;

    public QuoteToInvoiceConversionService(
        TenantDbContextFactory contextFactory,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        IAuditService auditService,
        ITenantContext tenantContext,
        IFiscalStampResolver fiscalStampResolver)
    {
        _contextFactory = contextFactory;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _auditService = auditService;
        _tenantContext = tenantContext;
        _fiscalStampResolver = fiscalStampResolver;
    }

    public async Task<Result<Guid>> ConvertAsync(
        Guid quoteId,
        ConvertQuoteToInvoiceDto? options,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var strategyContext = _contextFactory.CreateContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var context = _contextFactory.CreateContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var quote = await context.Quotes
                    .Include(q => q.Client)
                    .Include(q => q.Lines)
                    .ThenInclude(l => l.Product)
                    .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

                if (quote is null)
                    return Result.Failure<Guid>(Error.NotFound("Devis", quoteId));

                if (quote.Status != QuoteStatus.Accepted)
                    return Result.Failure<Guid>(Error.Validation("Status",
                        "Seuls les devis acceptés peuvent être transformés en facture"));

                if (quote.ConvertedInvoiceId.HasValue)
                    return Result.Failure<Guid>(Error.Conflict("Ce devis a déjà été transformé en facture"));

                if (!quote.Lines.Any())
                    return Result.Failure<Guid>(Error.Validation("Lines",
                        "Le devis doit contenir au moins une ligne"));

                if (options?.WarehouseId is { } warehouseId)
                {
                    var warehouse = await context.Warehouses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);
                    if (warehouse is null)
                        return Result.Failure<Guid>(Error.NotFound("Warehouse", warehouseId));
                    if (!warehouse.IsActive)
                        return Result.Failure<Guid>(Error.Validation("WarehouseId",
                            "L'entrepôt sélectionné n'est pas actif"));
                }

                var issueDate = options?.IssueDate ?? DateTime.UtcNow.Date;
                var dueDate = options?.DueDate;

                var tenantId = _tenantContext.TenantId
                    ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible.");
                var invoiceNumber = await _invoiceNumberGenerator.ReserveNextNumberAsync(
                    tenantId,
                    "FAC",
                    issueDate.Year,
                    cancellationToken);

                var invoiceResult = Invoice.CreateFromQuote(
                    invoiceNumber,
                    quote.Client,
                    issueDate,
                    quote.Id,
                    dueDate,
                    options?.Reference ?? quote.Reference,
                    options?.Notes ?? quote.Notes,
                    options?.PaymentTerms,
                    options?.WarehouseId);

                if (invoiceResult.IsFailure)
                    return Result.Failure<Guid>(invoiceResult.Error);

                var invoice = invoiceResult.Value;

                var issuerId = await context.Companies
                    .AsNoTracking()
                    .Where(c => c.IsDefault && c.IsActive)
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                invoice.SetIssuerCompanyId(issuerId);

                foreach (var line in quote.Lines.OrderBy(l => l.LineNumber))
                {
                    Result addResult;
                    if (line.ProductId != Guid.Empty && line.Product != null)
                    {
                        addResult = invoice.AddLine(
                            line.Product,
                            line.Quantity,
                            line.UnitPrice,
                            line.DiscountPercent);
                    }
                    else
                    {
                        addResult = invoice.AddCustomLine(
                            line.ProductName,
                            line.ProductDescription,
                            line.Quantity,
                            line.Unit ?? "unité",
                            line.UnitPrice,
                            line.VatRate,
                            line.DiscountPercent);
                    }

                    if (addResult.IsFailure)
                        return Result.Failure<Guid>(addResult.Error);
                }

                foreach (var mention in quote.LegalMentions)
                    invoice.AddLegalMention(mention);

                var stampMoney = await _fiscalStampResolver.ResolveSignedStampAsync(
                    isCreditNote: false,
                    cancellationToken);
                var stampResult = invoice.SetFiscalStampAmount(stampMoney);
                if (stampResult.IsFailure)
                    return Result.Failure<Guid>(stampResult.Error);

                invoice.SetAuditInfo(userId);
                quote.MarkAsConverted(invoice.Id);

                context.Invoices.Add(invoice);
                context.Quotes.Update(quote);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                await _auditService.LogAsync(
                    AuditActions.Quote.Converted,
                    "Quote",
                    quote.Id,
                    oldValues: new { quote.Number.Value, Status = "Accepted" },
                    newValues: new { quote.Number.Value, quote.Status, ConvertedInvoiceId = invoice.Id },
                    cancellationToken: cancellationToken);

                await _auditService.LogAsync(
                    AuditActions.Invoice.Created,
                    "Invoice",
                    invoice.Id,
                    newValues: new
                    {
                        invoice.Number.Value,
                        invoice.TotalAmount.Amount,
                        SourceQuoteId = quote.Id,
                        SourceQuoteNumber = quote.Number.Value
                    },
                    cancellationToken: cancellationToken);

                return Result.Success(invoice.Id);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
