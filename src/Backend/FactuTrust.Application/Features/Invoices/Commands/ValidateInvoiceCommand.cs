using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Invoices.Commands;

/// <summary>
/// Command to validate an invoice (finalize draft).
/// </summary>
public sealed record ValidateInvoiceCommand(Guid InvoiceId) : IRequest<Result>;

/// <summary>
/// Handler for ValidateInvoiceCommand.
/// </summary>
public sealed class ValidateInvoiceCommandHandler : IRequestHandler<ValidateInvoiceCommand, Result>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ValidateInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _invoiceRepository = invoiceRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ValidateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(request.InvoiceId, cancellationToken);

        if (invoice is null)
            return Result.Failure(Error.NotFound("Facture", request.InvoiceId));

        var result = invoice.Validate();

        if (result.IsFailure)
            return result;

        invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Invoice.Validated,
            "Invoice",
            invoice.Id,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
