using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Queries;

/// <summary>
/// Query to export a quote as PDF. <paramref name="TemplateKey"/> permet une surcharge du modèle.
/// </summary>
public sealed record ExportQuotePdfQuery(Guid QuoteId, string? TemplateKey = null) : IRequest<Result<QuotePdfResult>>;

/// <summary>
/// Result containing the PDF data.
/// </summary>
public sealed record QuotePdfResult(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Handler for ExportQuotePdfQuery.
/// </summary>
public sealed class ExportQuotePdfQueryHandler : IRequestHandler<ExportQuotePdfQuery, Result<QuotePdfResult>>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;
    private readonly IDocumentTemplateResolver _templateResolver;
    private readonly ICompanyRepository _companyRepository;

    public ExportQuotePdfQueryHandler(
        IQuoteRepository quoteRepository,
        IPdfService pdfService,
        IAuditService auditService,
        IDocumentTemplateResolver templateResolver,
        ICompanyRepository companyRepository)
    {
        _quoteRepository = quoteRepository;
        _pdfService = pdfService;
        _auditService = auditService;
        _templateResolver = templateResolver;
        _companyRepository = companyRepository;
    }

    public async Task<Result<QuotePdfResult>> Handle(ExportQuotePdfQuery request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdWithLinesAsync(request.QuoteId, cancellationToken);

        if (quote is null)
            return Result.Failure<QuotePdfResult>(Error.NotFound("Devis", request.QuoteId));

        var templateKey = await _templateResolver.ResolveAsync(PrintableDocumentType.Quote, request.TemplateKey, cancellationToken);
        var issuer = await _companyRepository.GetDefaultAsync(cancellationToken);
        var pdfBytes = await _pdfService.GenerateQuotePdfAsync(quote, issuer, templateKey, cancellationToken);
        var fileName = $"Devis_{quote.Number.Value}.pdf";

        await _auditService.LogAsync(
            AuditActions.Quote.Viewed,
            "Quote",
            quote.Id,
            cancellationToken: cancellationToken);

        return Result.Success(new QuotePdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
