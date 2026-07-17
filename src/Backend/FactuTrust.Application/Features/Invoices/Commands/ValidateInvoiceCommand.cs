using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Invoices.Commands;

/// <summary>
/// Command to validate an invoice (finalize draft).
/// </summary>
public sealed record ValidateInvoiceCommand(Guid InvoiceId) : IRequest<Result>;

/// <summary>
/// Handler for ValidateInvoiceCommand.
/// Transaction stricte : la facture et son écriture comptable sont persistées dans la MÊME
/// transaction (unité de travail ambiante). Si la génération de l'écriture échoue (compte
/// manquant, déséquilibre…), la validation est refusée et la facture reste en brouillon.
/// Un tenant sans plan comptable initialisé reste validable (la génération est alors un
/// succès « non applicable » — comportement historique préservé).
/// </summary>
public sealed class ValidateInvoiceCommandHandler : IRequestHandler<ValidateInvoiceCommand, Result>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IAccountingService _accountingService;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ValidateInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IAccountingService accountingService,
        ITenantUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _invoiceRepository = invoiceRepository;
        _accountingService = accountingService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ValidateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.ExecuteAsync(async ct =>
        {
            var invoice = await _invoiceRepository.GetByIdWithLinesAsync(request.InvoiceId, ct);

            if (invoice is null)
                return Result.Failure(Error.NotFound("Facture", request.InvoiceId));

            var validateResult = invoice.Validate();

            if (validateResult.IsFailure)
                return validateResult;

            invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

            await _invoiceRepository.UpdateAsync(invoice, ct);

            var entryResult = invoice.Type == InvoiceType.CreditNote
                ? await _accountingService.GenerateInvoiceCreditNoteEntryAsync(invoice, ct)
                : await _accountingService.GenerateInvoiceSaleEntryAsync(invoice, ct);

            if (entryResult.IsFailure)
                return Result.Failure(entryResult.Error);

            return Result.Success();
        }, cancellationToken);

        if (result.IsFailure)
            return result;

        // Audit hors transaction (chaîne hash-chaînée dans sa propre transaction Serializable).
        await _auditService.LogAsync(
            AuditActions.Invoice.Validated,
            "Invoice",
            request.InvoiceId,
            cancellationToken: cancellationToken);

        return result;
    }
}
