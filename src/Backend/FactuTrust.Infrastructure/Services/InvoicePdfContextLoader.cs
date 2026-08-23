using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Models;
using FactuTrust.Application.Features.Stock.Queries;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Infrastructure.Services;

public sealed class InvoicePdfContextLoader : IInvoicePdfContextLoader
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IQuoteRepository _quoteRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IStockTraceabilityQuery _traceability;

    public InvoicePdfContextLoader(
        ICompanyRepository companyRepository,
        IQuoteRepository quoteRepository,
        IInvoiceRepository invoiceRepository,
        IStockTraceabilityQuery traceability)
    {
        _companyRepository = companyRepository;
        _quoteRepository = quoteRepository;
        _invoiceRepository = invoiceRepository;
        _traceability = traceability;
    }

    public async Task<InvoicePdfContext> LoadAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        Company? issuer = null;
        if (invoice.IssuerCompanyId.HasValue)
            issuer = await _companyRepository.GetByIdAsync(invoice.IssuerCompanyId.Value, cancellationToken);

        issuer ??= await _companyRepository.GetDefaultAsync(cancellationToken);

        string? quoteNumber = null;
        if (invoice.SourceQuoteId.HasValue)
        {
            var quote = await _quoteRepository.GetByIdAsync(invoice.SourceQuoteId.Value, cancellationToken);
            quoteNumber = quote?.Number.Value;
        }

        // Numéro de la facture rectifiée : imprimé sur le PDF de l'avoir (exigence de
        // traçabilité — une facture rectificative doit référencer la facture d'origine).
        string? linkedInvoiceNumber = null;
        if (invoice.LinkedInvoiceId.HasValue)
        {
            var linked = await _invoiceRepository.GetByIdAsync(invoice.LinkedInvoiceId.Value, cancellationToken);
            linkedInvoiceNumber = linked?.Number.Value;
        }

        var lineIds = invoice.Lines.Select(l => l.Id).ToList();
        var kind = invoice.Type == InvoiceType.CreditNote ? StockDocumentKind.CreditNote : StockDocumentKind.Invoice;
        var lotLabels = await _traceability.GetLotLabelsAsync(kind, lineIds, cancellationToken);

        return new InvoicePdfContext(invoice, issuer, quoteNumber, linkedInvoiceNumber, lotLabels);
    }
}
