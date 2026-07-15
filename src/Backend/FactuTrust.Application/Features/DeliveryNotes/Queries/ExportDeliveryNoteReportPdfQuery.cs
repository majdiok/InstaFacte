using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Invoices.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Queries;

/// <summary>
/// Query to export the delivery notes report as PDF for a given period and optional client.
/// </summary>
public sealed record ExportDeliveryNoteReportPdfQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    Guid? ClientId = null) : IRequest<Result<InvoicePdfResult>>;

/// <summary>
/// Handler for ExportDeliveryNoteReportPdfQuery.
/// </summary>
public sealed class ExportDeliveryNoteReportPdfQueryHandler : IRequestHandler<ExportDeliveryNoteReportPdfQuery, Result<InvoicePdfResult>>
{
    private const int MaxDeliveryNotesForReport = 1000;

    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;

    public ExportDeliveryNoteReportPdfQueryHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        ICompanyRepository companyRepository,
        IClientRepository clientRepository,
        IPdfService pdfService,
        IAuditService auditService)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _companyRepository = companyRepository;
        _clientRepository = clientRepository;
        _pdfService = pdfService;
        _auditService = auditService;
    }

    public async Task<Result<InvoicePdfResult>> Handle(ExportDeliveryNoteReportPdfQuery request, CancellationToken cancellationToken)
    {
        if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value > request.ToDate.Value)
            return Result.Failure<InvoicePdfResult>(Error.Validation("Period", "La date de début doit être antérieure à la date de fin."));

        var deliveryNotes = await _deliveryNoteRepository.GetForReportAsync(
            request.FromDate,
            request.ToDate,
            request.ClientId,
            cancellationToken);

        if (deliveryNotes.Count == 0)
            return Result.Failure<InvoicePdfResult>(new Error("NoDeliveryNotes", "Aucun bon de livraison pour cette période."));

        if (deliveryNotes.Count > MaxDeliveryNotesForReport)
            return Result.Failure<InvoicePdfResult>(new Error("TooManyDeliveryNotes", "Trop de bons. Réduisez la période."));

        var company = await _companyRepository.GetDefaultAsync(cancellationToken);

        string? clientName = null;
        if (request.ClientId.HasValue)
        {
            var client = await _clientRepository.GetByIdAsync(request.ClientId.Value, cancellationToken);
            clientName = client?.Name;
        }

        var pdfBytes = await _pdfService.GenerateDeliveryNoteReportPdfAsync(
            deliveryNotes,
            request.FromDate,
            request.ToDate,
            company,
            clientName,
            cancellationToken);

        var fileName = request.FromDate.HasValue && request.ToDate.HasValue
            ? $"Etat_bons_livraison_{request.FromDate.Value:yyyy-MM-dd}_{request.ToDate.Value:yyyy-MM-dd}.pdf"
            : (!request.FromDate.HasValue && !request.ToDate.HasValue
                ? "Etat_bons_livraison_toutes.pdf"
                : (!request.FromDate.HasValue
                    ? $"Etat_bons_livraison_jusquau_{request.ToDate!.Value:yyyy-MM-dd}.pdf"
                    : $"Etat_bons_livraison_depuisle_{request.FromDate!.Value:yyyy-MM-dd}.pdf"));

        await _auditService.LogAsync(
            AuditActions.Export.Pdf,
            "DeliveryNoteReport",
            null,
            cancellationToken: cancellationToken);

        return Result.Success(new InvoicePdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
