using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Invoices.Commands;

/// <summary>
/// Command to electronically sign an invoice.
/// </summary>
public sealed record SignInvoiceCommand(Guid InvoiceId) : IRequest<Result<string>>;

/// <summary>
/// Handler for SignInvoiceCommand.
/// </summary>
public sealed class SignInvoiceCommandHandler : IRequestHandler<SignInvoiceCommand, Result<string>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ISignatureService _signatureService;
    private readonly IPdfService _pdfService;
    private readonly IInvoicePdfContextLoader _invoicePdfContextLoader;
    private readonly IAuditService _auditService;

    public SignInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ISignatureService signatureService,
        IPdfService pdfService,
        IInvoicePdfContextLoader invoicePdfContextLoader,
        IAuditService auditService)
    {
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _signatureService = signatureService;
        _pdfService = pdfService;
        _invoicePdfContextLoader = invoicePdfContextLoader;
        _auditService = auditService;
    }

    public async Task<Result<string>> Handle(SignInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(request.InvoiceId, cancellationToken);
        
        if (invoice is null)
            return Result.Failure<string>(Error.NotFound("Facture", request.InvoiceId));

        // Generate PDF to sign
        var pdfContext = await _invoicePdfContextLoader.LoadAsync(invoice, cancellationToken);
        var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(pdfContext, cancellationToken);
        
        // Sign the PDF content
        var signatureHash = await _signatureService.SignAsync(pdfBytes, cancellationToken);
        
        var signedBy = _currentUser.Email ?? "system";
        var result = invoice.Sign(signatureHash, signedBy);
        
        if (result.IsFailure)
            return Result.Failure<string>(result.Error);

        invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Invoice.Signed,
            "Invoice",
            invoice.Id,
            newValues: new { SignatureHash = signatureHash, SignedBy = signedBy },
            cancellationToken: cancellationToken);

        return Result.Success(signatureHash);
    }
}
