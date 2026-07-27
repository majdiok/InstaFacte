using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Commands;

/// <summary>
/// Generates an invoice from a delivery note, using only delivered quantities
/// and the product snapshot data (prices, VAT) captured at BL creation time.
/// </summary>
public sealed record GenerateInvoiceFromDeliveryNoteCommand(
    Guid DeliveryNoteId,
    GenerateInvoiceFromDeliveryNoteDto Dto) : IRequest<Result<Guid>>;

/// <summary>
/// Handler for GenerateInvoiceFromDeliveryNoteCommand.
/// </summary>
public sealed class GenerateInvoiceFromDeliveryNoteCommandHandler
    : IRequestHandler<GenerateInvoiceFromDeliveryNoteCommand, Result<Guid>>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IFiscalStampResolver _fiscalStampResolver;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly ITenantContext _tenantContext;

    public GenerateInvoiceFromDeliveryNoteCommandHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        IInvoiceRepository invoiceRepository,
        IFiscalStampResolver fiscalStampResolver,
        IInvoiceNumberGenerator numberGenerator,
        ITenantContext tenantContext)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _invoiceRepository = invoiceRepository;
        _fiscalStampResolver = fiscalStampResolver;
        _numberGenerator = numberGenerator;
        _tenantContext = tenantContext;
    }

    public async Task<Result<Guid>> Handle(
        GenerateInvoiceFromDeliveryNoteCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Load delivery note with lines and client
        var deliveryNote = await _deliveryNoteRepository.GetByIdWithDetailsAsync(
            request.DeliveryNoteId, cancellationToken);

        if (deliveryNote is null)
            return Result.Failure<Guid>(Error.NotFound("DeliveryNote", request.DeliveryNoteId));

        // 2. Validate status — only Delivered or PartiallyDelivered can be invoiced
        if (!deliveryNote.Status.CanBeInvoiced())
            return Result.Failure<Guid>(Error.Validation("Status",
                $"Ce bon de livraison ne peut pas être facturé (statut actuel : {deliveryNote.Status.ToDisplayString()})"));

        // 3. Double-generation protection
        if (deliveryNote.InvoiceId.HasValue)
            return Result.Failure<Guid>(Error.Validation("Invoice",
                "Ce bon de livraison a déjà été facturé"));

        // 4. Collect lines with delivered quantity > 0
        var deliveredLines = deliveryNote.Lines
            .Where(l => l.DeliveredQuantity > 0)
            .OrderBy(l => l.LineNumber)
            .ToList();

        if (deliveredLines.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines",
                "Aucune ligne avec une quantité livrée — impossible de générer une facture"));

        // 5. Generate invoice number (atomic sequence)
        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var dto = request.Dto;
        var issueDate = dto.IssueDate ?? DateTime.UtcNow.Date;
        var invoiceNumber = await _numberGenerator.ReserveNextNumberAsync(
            tenantId.Value, "FAC", issueDate.Year, cancellationToken);

        // 6. Create invoice using the BL's client
        var client = deliveryNote.Client!;
        var invoiceResult = Invoice.CreateFromDeliveryNote(
            invoiceNumber,
            client,
            issueDate,
            deliveryNote.Id,
            dto.DueDate,
            dto.Reference ?? $"BL {deliveryNote.Number.Value}",
            dto.Notes,
            paymentTerms: null,
            warehouseId: deliveryNote.WarehouseId);

        if (invoiceResult.IsFailure)
            return Result.Failure<Guid>(invoiceResult.Error);

        var invoice = invoiceResult.Value;

        // 7. Add invoice lines from BL delivered quantities using snapshot prices.
        //    Products are already loaded via GetByIdWithDetailsAsync (.ThenInclude),
        //    reusing the same instances avoids EF Core tracking conflicts across contexts.
        foreach (var blLine in deliveredLines)
        {
            var product = blLine.Product;
            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Produit", blLine.ProductId));

            var snapshotPrice = Money.Create(blLine.UnitPriceHT);

            var addLineResult = invoice.AddLine(
                product,
                blLine.DeliveredQuantity,
                snapshotPrice);

            if (addLineResult.IsFailure)
                return Result.Failure<Guid>(addLineResult.Error);
        }

        var stampMoney = await _fiscalStampResolver.ResolveSignedStampAsync(isCreditNote: false, cancellationToken);
        var stampResult = invoice.SetFiscalStampAmount(stampMoney);
        if (stampResult.IsFailure)
            return Result.Failure<Guid>(stampResult.Error);

        // 8. Save invoice
        await _invoiceRepository.AddAsync(invoice, cancellationToken);

        // 9. Mark delivery note as invoiced
        var markResult = deliveryNote.MarkAsInvoiced(invoice);
        if (markResult.IsFailure)
            return Result.Failure<Guid>(markResult.Error);

        // 10. Persist delivery note update (InvoiceId, Status, InvoicedAt)
        await _deliveryNoteRepository.UpdateAsync(deliveryNote, cancellationToken);

        return Result.Success(invoice.Id);
    }
}
