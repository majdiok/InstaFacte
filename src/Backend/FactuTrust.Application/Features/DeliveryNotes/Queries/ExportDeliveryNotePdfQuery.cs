using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Queries;

/// <summary>
/// Exporte un bon de livraison individuel en PDF (modèle configurable, surcharge possible).
/// </summary>
public sealed record ExportDeliveryNotePdfQuery(Guid DeliveryNoteId, string? TemplateKey = null)
    : IRequest<Result<DeliveryNotePdfResult>>;

public sealed record DeliveryNotePdfResult(byte[] Content, string FileName, string ContentType);

public sealed class ExportDeliveryNotePdfQueryHandler
    : IRequestHandler<ExportDeliveryNotePdfQuery, Result<DeliveryNotePdfResult>>
{
    private readonly IDeliveryNoteRepository _repository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;
    private readonly IDocumentTemplateResolver _templateResolver;
    private readonly ICompanyRepository _companyRepository;

    public ExportDeliveryNotePdfQueryHandler(
        IDeliveryNoteRepository repository,
        IPdfService pdfService,
        IAuditService auditService,
        IDocumentTemplateResolver templateResolver,
        ICompanyRepository companyRepository)
    {
        _repository = repository;
        _pdfService = pdfService;
        _auditService = auditService;
        _templateResolver = templateResolver;
        _companyRepository = companyRepository;
    }

    public async Task<Result<DeliveryNotePdfResult>> Handle(ExportDeliveryNotePdfQuery request, CancellationToken cancellationToken)
    {
        var deliveryNote = await _repository.GetByIdWithDetailsAsync(request.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure<DeliveryNotePdfResult>(Error.NotFound("Bon de livraison", request.DeliveryNoteId));

        var templateKey = await _templateResolver.ResolveAsync(PrintableDocumentType.DeliveryNote, request.TemplateKey, cancellationToken);
        var issuer = await _companyRepository.GetDefaultAsync(cancellationToken);
        var pdfBytes = await _pdfService.GenerateDeliveryNotePdfAsync(deliveryNote, issuer, templateKey, cancellationToken);

        var fileName = $"BL_{deliveryNote.Number.Value}.pdf";

        await _auditService.LogAsync(AuditActions.DeliveryNote.Exported, "DeliveryNote", deliveryNote.Id, cancellationToken: cancellationToken);

        return Result.Success(new DeliveryNotePdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
