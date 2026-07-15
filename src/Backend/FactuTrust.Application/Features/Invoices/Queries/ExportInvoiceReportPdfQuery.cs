using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Invoices.Queries;

/// <summary>
/// Query to export the sales invoices report as PDF for a given period and optional client.
/// </summary>
public sealed record ExportInvoiceReportPdfQuery(DateTime? FromDate, DateTime? ToDate, Guid? ClientId = null) : IRequest<Result<InvoicePdfResult>>;

/// <summary>
/// Handler for ExportInvoiceReportPdfQuery.
/// </summary>
public sealed class ExportInvoiceReportPdfQueryHandler : IRequestHandler<ExportInvoiceReportPdfQuery, Result<InvoicePdfResult>>
{
    private const int MaxInvoicesForReport = 1000;

    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;

    public ExportInvoiceReportPdfQueryHandler(
        IInvoiceRepository invoiceRepository,
        ICompanyRepository companyRepository,
        IClientRepository clientRepository,
        IPdfService pdfService,
        IAuditService auditService)
    {
        _invoiceRepository = invoiceRepository;
        _companyRepository = companyRepository;
        _clientRepository = clientRepository;
        _pdfService = pdfService;
        _auditService = auditService;
    }

    public async Task<Result<InvoicePdfResult>> Handle(ExportInvoiceReportPdfQuery request, CancellationToken cancellationToken)
    {
        if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value > request.ToDate.Value)
            return Result.Failure<InvoicePdfResult>(Error.Validation("Period", "La date de début doit être antérieure à la date de fin."));

        var invoices = await _invoiceRepository.GetForReportAsync(request.FromDate, request.ToDate, request.ClientId, cancellationToken);

        var nonCancelled = invoices
            .Where(i => i.Status != InvoiceStatus.Cancelled)
            .ToList();

        if (nonCancelled.Count == 0)
            return Result.Failure<InvoicePdfResult>(new Error("NoInvoices", "Aucune facture pour cette période."));

        if (nonCancelled.Count > MaxInvoicesForReport)
            return Result.Failure<InvoicePdfResult>(new Error("TooManyInvoices", "Trop de factures. Réduisez la période."));

        var company = await _companyRepository.GetDefaultAsync(cancellationToken);

        string? clientName = null;
        if (request.ClientId.HasValue)
        {
            var client = await _clientRepository.GetByIdAsync(request.ClientId.Value, cancellationToken);
            clientName = client?.Name;
        }

        var pdfBytes = await _pdfService.GenerateInvoiceReportPdfAsync(
            nonCancelled,
            request.FromDate,
            request.ToDate,
            company,
            clientName);
        
        var fileName = request.FromDate.HasValue && request.ToDate.HasValue
            ? $"Etat_factures_ventes_{request.FromDate.Value:yyyy-MM-dd}_{request.ToDate.Value:yyyy-MM-dd}.pdf"
            : (!request.FromDate.HasValue && !request.ToDate.HasValue
                ? "Etat_factures_ventes_toutes.pdf"
                : (!request.FromDate.HasValue
                    ? $"Etat_factures_ventes_jusquau_{request.ToDate!.Value:yyyy-MM-dd}.pdf"
                    : $"Etat_factures_ventes_depuisle_{request.FromDate!.Value:yyyy-MM-dd}.pdf"));

        await _auditService.LogAsync(
            AuditActions.Export.Pdf,
            "InvoiceReport",
            null,
            cancellationToken: cancellationToken);

        return Result.Success(new InvoicePdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
