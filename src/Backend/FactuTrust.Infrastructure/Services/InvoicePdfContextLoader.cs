using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Models;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Infrastructure.Services;

public sealed class InvoicePdfContextLoader : IInvoicePdfContextLoader
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IQuoteRepository _quoteRepository;
    private readonly IInvoiceRepository _invoiceRepository;

    public InvoicePdfContextLoader(
        ICompanyRepository companyRepository,
        IQuoteRepository quoteRepository,
        IInvoiceRepository invoiceRepository)
    {
        _companyRepository = companyRepository;
        _quoteRepository = quoteRepository;
        _invoiceRepository = invoiceRepository;
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

        return new InvoicePdfContext(invoice, issuer, quoteNumber, linkedInvoiceNumber);
    }
}
