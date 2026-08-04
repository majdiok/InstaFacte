using System.Text.RegularExpressions;

using FactuTrust.Domain.Entities;

using FactuTrust.Domain.Enums;

using FactuTrust.Domain.Services;

using FactuTrust.Domain.ValueObjects;

using FactuTrust.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;



namespace FactuTrust.Infrastructure.Services;



public sealed class DocumentNumberService : IDocumentNumberService

{

    private readonly IDbContextFactory<TenantDbContext> _contextFactory;

    private readonly ILogger<DocumentNumberService> _logger;



    public DocumentNumberService(

        IDbContextFactory<TenantDbContext> contextFactory,

        ILogger<DocumentNumberService> logger)

    {

        _contextFactory = contextFactory;

        _logger = logger;

    }



    public async Task<DocumentNumberResult> ReserveNextAsync(

        Guid tenantId,

        NumberingDocumentType documentType,

        int fiscalYear,

        DateTime referenceDate,

        CancellationToken cancellationToken = default)

    {

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var strategy = context.Database.CreateExecutionStrategy();



        return await strategy.ExecuteAsync(async () =>

        {

            await using var transaction = await context.Database.BeginTransactionAsync(

                System.Data.IsolationLevel.Serializable, cancellationToken);



            try

            {

                var scheme = await GetOrCreateSchemeTrackedAsync(

                    context, tenantId, documentType, fiscalYear, cancellationToken);



                var sequence = scheme.ReserveNextSequence();

                var blocks = scheme.GetBlocks();

                // Resilience: a persisted format that cannot render (e.g. missing the document-number
                // block) would otherwise throw here and block ALL document emission for the tenant.
                // Self-heal the scheme to the default format for this document type instead of failing.
                // The sequence counters are preserved, so numbering continuity is unaffected, and valid
                // formats pass through untouched (ValidateBlocks is a no-op for them).
                if (NumberingFormatRenderer.ValidateBlocks(blocks).IsFailure)
                {
                    _logger.LogWarning(
                        "Invalid numbering format for tenant {TenantId}, {DocumentType}, year {FiscalYear} - " +
                        "self-healing to the default format. Reconfigure it in Settings > Numbering if needed.",
                        tenantId, documentType, fiscalYear);

                    scheme.RepairFormatToDefault();
                    blocks = scheme.GetBlocks();
                }

                var freeText = blocks.FirstOrDefault(b => b.Type == NumberingBlockType.FreeText)?.Value

                    ?? documentType.DefaultFreeText();



                var renderResult = NumberingFormatRenderer.Render(

                    blocks,

                    new NumberingFormatRenderer.RenderContext(sequence, referenceDate, freeText));



                if (renderResult.IsFailure)

                    throw new InvalidOperationException(renderResult.Error.Description);



                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);



                _logger.LogInformation(

                    "Reserved {DocumentType} number {Number} for tenant {TenantId}",

                    documentType, renderResult.Value, tenantId);



                return new DocumentNumberResult(

                    renderResult.Value,

                    referenceDate.Year,

                    sequence,

                    freeText);

            }

            catch

            {

                await transaction.RollbackAsync(cancellationToken);

                throw;

            }

        });

    }



    public async Task<string> PreviewNextAsync(

        Guid tenantId,

        NumberingDocumentType documentType,

        int fiscalYear,

        DateTime referenceDate,

        CancellationToken cancellationToken = default)

    {

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);



        var scheme = await context.DocumentNumberingSchemes

            .AsNoTracking()

            .Where(s => s.TenantId == tenantId && s.DocumentType == documentType && s.FiscalYear == fiscalYear)

            .FirstOrDefaultAsync(cancellationToken);



        var effectiveSequence = await GetEffectiveCurrentSequenceAsync(

            context, tenantId, documentType, fiscalYear, cancellationToken);



        var blocks = scheme?.GetBlocks() ?? NumberingSchemeDefaults.GetDefaultBlocks(documentType);

        var freeText = blocks.FirstOrDefault(b => b.Type == NumberingBlockType.FreeText)?.Value

            ?? documentType.DefaultFreeText();

        var startNumber = scheme?.StartNumber ?? Math.Max(1, effectiveSequence + 1);

        var nextSequence = Math.Max(effectiveSequence + 1, startNumber);



        var render = NumberingFormatRenderer.Render(

            blocks,

            new NumberingFormatRenderer.RenderContext(nextSequence, referenceDate, freeText));

        return render.IsSuccess ? render.Value : string.Empty;

    }



    public async Task<int> GetEffectiveCurrentSequenceAsync(

        Guid tenantId,

        NumberingDocumentType documentType,

        int fiscalYear,

        CancellationToken cancellationToken = default)

    {

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await GetEffectiveCurrentSequenceAsync(

            context, tenantId, documentType, fiscalYear, cancellationToken);

    }



    private static async Task<int> GetEffectiveCurrentSequenceAsync(

        TenantDbContext context,

        Guid tenantId,

        NumberingDocumentType documentType,

        int fiscalYear,

        CancellationToken cancellationToken)

    {

        var schemeSequence = await context.DocumentNumberingSchemes

            .AsNoTracking()

            .Where(s => s.TenantId == tenantId && s.DocumentType == documentType && s.FiscalYear == fiscalYear)

            .Select(s => (int?)s.CurrentSequence)

            .FirstOrDefaultAsync(cancellationToken) ?? 0;



        var legacySequence = await GetLegacyCurrentSequenceAsync(

            context, tenantId, documentType, fiscalYear, cancellationToken);



        var documentSequence = await GetMaxDocumentSequenceAsync(

            context, documentType, fiscalYear, cancellationToken);



        return Math.Max(schemeSequence, Math.Max(legacySequence, documentSequence));

    }



    private static async Task<DocumentNumberingScheme> GetOrCreateSchemeTrackedAsync(

        TenantDbContext context,

        Guid tenantId,

        NumberingDocumentType documentType,

        int fiscalYear,

        CancellationToken cancellationToken)

    {

        var scheme = await context.DocumentNumberingSchemes

            .Where(s => s.TenantId == tenantId && s.DocumentType == documentType && s.FiscalYear == fiscalYear)

            .FirstOrDefaultAsync(cancellationToken);



        if (scheme is not null)

            return scheme;



        var legacySequence = await GetLegacyCurrentSequenceAsync(

            context, tenantId, documentType, fiscalYear, cancellationToken);

        var documentSequence = await GetMaxDocumentSequenceAsync(

            context, documentType, fiscalYear, cancellationToken);

        var effectiveSequence = Math.Max(legacySequence, documentSequence);



        scheme = DocumentNumberingScheme.CreateDefault(tenantId, documentType, fiscalYear, effectiveSequence);

        context.DocumentNumberingSchemes.Add(scheme);

        return scheme;

    }



    private static async Task<int> GetLegacyCurrentSequenceAsync(

        TenantDbContext context,

        Guid tenantId,

        NumberingDocumentType documentType,

        int fiscalYear,

        CancellationToken cancellationToken)

    {

        var prefix = documentType.LegacyPrefix();



        return documentType switch

        {

            NumberingDocumentType.Invoice or NumberingDocumentType.CreditNote =>

                await context.InvoiceNumberSequences.AsNoTracking()

                    .Where(s => s.TenantId == tenantId && s.Prefix == prefix && s.FiscalYear == fiscalYear)

                    .Select(s => (int?)s.CurrentSequence)

                    .FirstOrDefaultAsync(cancellationToken) ?? 0,



            NumberingDocumentType.Quote =>

                await context.QuoteNumberSequences.AsNoTracking()

                    .Where(s => s.TenantId == tenantId && s.Prefix == prefix && s.FiscalYear == fiscalYear)

                    .Select(s => (int?)s.CurrentSequence)

                    .FirstOrDefaultAsync(cancellationToken) ?? 0,



            NumberingDocumentType.CashReceipt or NumberingDocumentType.CashExpense =>

                await context.CashOperationNumberSequences.AsNoTracking()

                    .Where(s => s.TenantId == tenantId && s.Prefix == prefix && s.FiscalYear == fiscalYear)

                    .Select(s => (int?)s.CurrentSequence)

                    .FirstOrDefaultAsync(cancellationToken) ?? 0,



            NumberingDocumentType.BankDeposit =>

                await context.BankDepositNumberSequences.AsNoTracking()

                    .Where(s => s.TenantId == tenantId && s.FiscalYear == fiscalYear)

                    .Select(s => (int?)s.CurrentSequence)

                    .FirstOrDefaultAsync(cancellationToken) ?? 0,



            NumberingDocumentType.PhysicalInventory =>

                await context.InventoryNumberSequences.AsNoTracking()

                    .Where(s => s.Year == fiscalYear)

                    .Select(s => (int?)s.LastSequence)

                    .FirstOrDefaultAsync(cancellationToken) ?? 0,



            _ => 0

        };

    }



    private static async Task<int> GetMaxDocumentSequenceAsync(

        TenantDbContext context,

        NumberingDocumentType documentType,

        int fiscalYear,

        CancellationToken cancellationToken)

    {

        return documentType switch

        {

            NumberingDocumentType.Invoice =>

                await context.Invoices.AsNoTracking()

                    .Where(i => i.Number.Year == fiscalYear && i.Type == InvoiceType.Standard)

                    .Select(i => (int?)i.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.CreditNote =>

                await context.Invoices.AsNoTracking()

                    .Where(i => i.Number.Year == fiscalYear && i.Type == InvoiceType.CreditNote)

                    .Select(i => (int?)i.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.Quote =>

                await context.Quotes.AsNoTracking()

                    .Where(q => q.Number.Year == fiscalYear)

                    .Select(q => (int?)q.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.DeliveryNote =>

                await context.DeliveryNotes.AsNoTracking()

                    .Where(d => d.Number.Year == fiscalYear)

                    .Select(d => (int?)d.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.PurchaseOrder =>

                await context.PurchaseOrders.AsNoTracking()

                    .Where(p => p.Number.Year == fiscalYear)

                    .Select(p => (int?)p.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.PurchaseReceipt =>

                await context.PurchaseReceipts.AsNoTracking()

                    .Where(p => p.Number.Year == fiscalYear)

                    .Select(p => (int?)p.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.StockTransfer =>

                await context.StockTransfers.AsNoTracking()

                    .Where(t => t.Number.Year == fiscalYear)

                    .Select(t => (int?)t.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.CashReceipt =>

                await context.CashOperations.AsNoTracking()

                    .Where(c => c.Number.Year == fiscalYear && c.Number.PrefixValue == CashOperationNumber.CreditPrefix)

                    .Select(c => (int?)c.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.CashExpense =>

                await context.CashOperations.AsNoTracking()

                    .Where(c => c.Number.Year == fiscalYear && c.Number.PrefixValue == CashOperationNumber.DebitPrefix)

                    .Select(c => (int?)c.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.BankDeposit =>

                await context.BankDeposits.AsNoTracking()

                    .Where(b => b.Number.Year == fiscalYear)

                    .Select(b => (int?)b.Number.Sequence)

                    .MaxAsync(cancellationToken) ?? 0,



            NumberingDocumentType.PhysicalInventory =>

                await context.InventoryNumberSequences.AsNoTracking()

                    .Where(s => s.Year == fiscalYear)

                    .Select(s => (int?)s.LastSequence)

                    .FirstOrDefaultAsync(cancellationToken) ?? 0,



            NumberingDocumentType.SupplierInvoice =>

                await GetMaxSupplierInvoiceSequenceAsync(context, fiscalYear, cancellationToken),



            _ => 0

        };

    }



    private static readonly Regex SupplierInvoiceSequenceRegex =

        new(@"^FS-(\d{4})-(\d{6})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);



    private static async Task<int> GetMaxSupplierInvoiceSequenceAsync(

        TenantDbContext context,

        int fiscalYear,

        CancellationToken cancellationToken)

    {

        var numbers = await context.SupplierInvoices.AsNoTracking()

            .Where(si => si.InvoiceDate.Year == fiscalYear)

            .Select(si => si.InvoiceNumber)

            .ToListAsync(cancellationToken);



        var maxSequence = 0;

        foreach (var number in numbers)

        {

            if (TryParseSupplierInvoiceSequence(number, fiscalYear, out var sequence))

                maxSequence = Math.Max(maxSequence, sequence);

        }



        return maxSequence;

    }



    private static bool TryParseSupplierInvoiceSequence(string invoiceNumber, int fiscalYear, out int sequence)

    {

        var match = SupplierInvoiceSequenceRegex.Match(invoiceNumber?.Trim() ?? "");

        if (!match.Success || int.Parse(match.Groups[1].Value) != fiscalYear)

        {

            sequence = 0;

            return false;

        }



        sequence = int.Parse(match.Groups[2].Value);

        return true;

    }

}


