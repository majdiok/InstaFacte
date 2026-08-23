using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Queries;

public sealed record ExportSalesReturnNotePdfQuery(Guid Id, string? TemplateKey = null)
    : IRequest<Result<SalesReturnNotePdfResult>>;

public sealed record SalesReturnNotePdfResult(byte[] Content, string FileName, string ContentType);

public sealed class ExportSalesReturnNotePdfQueryHandler
    : IRequestHandler<ExportSalesReturnNotePdfQuery, Result<SalesReturnNotePdfResult>>
{
    private readonly ISalesReturnNoteRepository _repository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;
    private readonly IDocumentTemplateResolver _templateResolver;
    private readonly ICompanyRepository _companyRepository;

    public ExportSalesReturnNotePdfQueryHandler(
        ISalesReturnNoteRepository repository,
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

    public async Task<Result<SalesReturnNotePdfResult>> Handle(
        ExportSalesReturnNotePdfQuery request,
        CancellationToken cancellationToken)
    {
        var note = await _repository.GetByIdWithDetailsAsync(request.Id, cancellationToken);
        if (note is null)
            return Result.Failure<SalesReturnNotePdfResult>(Error.NotFound("SalesReturnNote", request.Id));

        var templateKey = await _templateResolver.ResolveAsync(
            PrintableDocumentType.SalesReturnNote, request.TemplateKey, cancellationToken);
        var issuer = await _companyRepository.GetDefaultAsync(cancellationToken);
        var pdfBytes = await _pdfService.GenerateSalesReturnNotePdfAsync(
            note, issuer, templateKey, cancellationToken);

        var fileName = $"BRT_{note.Number.Value}.pdf";

        await _auditService.LogAsync(
            AuditActions.SalesReturnNote.Exported,
            "SalesReturnNote",
            note.Id,
            cancellationToken: cancellationToken);

        return Result.Success(new SalesReturnNotePdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
