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
/// Facturation groupée : agrège plusieurs bons de livraison d'un MÊME client en une facture
/// unique — la facturation périodique attendue de tout distributeur B2B.
///
/// Le contrat <see cref="GenerateInvoiceFromDeliveryNotesDto"/> existait depuis longtemps sans
/// implémentation, tout comme le drapeau <c>DeliveryNote.AllowGroupInvoicing</c>.
///
/// ⚠️ Le stock a déjà été sorti à chaque livraison. La facture produite est marquée comme
/// issue de bons de livraison, de sorte que sa validation ne redéduise rien.
/// </summary>
public sealed record GenerateInvoiceFromDeliveryNotesCommand(
    GenerateInvoiceFromDeliveryNotesDto Dto) : IRequest<Result<Guid>>;

public sealed class GenerateInvoiceFromDeliveryNotesCommandHandler
    : IRequestHandler<GenerateInvoiceFromDeliveryNotesCommand, Result<Guid>>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IFiscalStampResolver _fiscalStampResolver;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ITenantUnitOfWork _unitOfWork;

    public GenerateInvoiceFromDeliveryNotesCommandHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        IInvoiceRepository invoiceRepository,
        IFiscalStampResolver fiscalStampResolver,
        IInvoiceNumberGenerator numberGenerator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAuditService auditService,
        ITenantUnitOfWork unitOfWork)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _invoiceRepository = invoiceRepository;
        _fiscalStampResolver = fiscalStampResolver;
        _numberGenerator = numberGenerator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<Guid>> Handle(
        GenerateInvoiceFromDeliveryNotesCommand request, CancellationToken cancellationToken)
        => _unitOfWork.ExecuteAsync(ct => HandleCoreAsync(request, ct), cancellationToken);

    private async Task<Result<Guid>> HandleCoreAsync(
        GenerateInvoiceFromDeliveryNotesCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (dto.DeliveryNoteIds is null || dto.DeliveryNoteIds.Count == 0)
            return Result.Failure<Guid>(Error.Validation("DeliveryNoteIds", "Aucun bon de livraison sélectionné"));

        var distinctIds = dto.DeliveryNoteIds.Distinct().ToList();

        var notes = new List<DeliveryNote>();
        foreach (var id in distinctIds)
        {
            var note = await _deliveryNoteRepository.GetByIdWithDetailsAsync(id, cancellationToken);
            if (note is null)
                return Result.Failure<Guid>(Error.NotFound("DeliveryNote", id));

            notes.Add(note);
        }

        // Un seul client : facturer plusieurs clients sur une facture n'a aucun sens fiscal.
        var clientIds = notes.Select(n => n.ClientId).Distinct().ToList();
        if (clientIds.Count > 1)
        {
            return Result.Failure<Guid>(Error.Validation("Client",
                "Les bons de livraison sélectionnés appartiennent à des clients différents"));
        }

        foreach (var note in notes)
        {
            if (note.InvoiceId.HasValue)
                return Result.Failure<Guid>(Error.Validation("Invoice",
                    $"Le bon de livraison {note.Number.Value} a déjà été facturé"));

            if (!note.Status.CanBeInvoiced())
                return Result.Failure<Guid>(Error.Validation("Status",
                    $"Le bon de livraison {note.Number.Value} ne peut pas être facturé " +
                    $"(statut : {note.Status.ToDisplayString()})"));

            if (!note.HasInvoiceableQuantity)
                return Result.Failure<Guid>(Error.Validation("Lines",
                    $"Le bon de livraison {note.Number.Value} n'a plus de quantité facturable (retours)."));
        }

        var deliveredLines = notes
            .SelectMany(n => n.Lines.Where(l => l.InvoiceableQuantity > 0))
            .ToList();

        if (deliveredLines.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines",
                "Toutes les quantités livrées ont été retournées — impossible de facturer."));

        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var issueDate = dto.IssueDate ?? DateTime.UtcNow.Date;
        var invoiceNumber = await _numberGenerator.ReserveNextNumberAsync(
            tenantId.Value, "FAC", issueDate.Year, cancellationToken);

        var orderedNotes = notes.OrderBy(n => n.Number.Value).ToList();
        var reference = dto.Reference
            ?? $"BL {string.Join(", ", orderedNotes.Select(n => n.Number.Value))}";

        // Entrepôt : conservé s'il est unique, sinon laissé au défaut — une facture groupée
        // peut couvrir plusieurs dépôts.
        var warehouseIds = notes.Select(n => n.WarehouseId).Distinct().ToList();
        var warehouseId = warehouseIds.Count == 1 ? warehouseIds[0] : null;

        var invoiceResult = Invoice.Create(
            invoiceNumber,
            orderedNotes[0].Client!,
            issueDate,
            dto.DueDate,
            reference,
            dto.Notes,
            paymentTerms: null,
            warehouseId: warehouseId);

        if (invoiceResult.IsFailure)
            return Result.Failure<Guid>(invoiceResult.Error);

        var invoice = invoiceResult.Value;

        // Marque l'origine « bons de livraison » : le stock est déjà sorti, la validation ne
        // doit rien redéduire.
        var markResult = invoice.MarkGeneratedFromDeliveryNotes(orderedNotes.Select(n => n.Id).ToList());
        if (markResult.IsFailure)
            return Result.Failure<Guid>(markResult.Error);

        // Remonte jusqu'à la commande quand les bons en proviennent tous.
        var salesOrderIds = notes
            .Select(n => n.SourceSalesOrderId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (salesOrderIds.Count == 1)
            invoice.AttachSalesOrderOrigin(salesOrderIds[0]);

        // Regroupement par produit / prix / remise : deux livraisons du même article au même
        // prix ne doivent pas produire deux lignes sur la facture mensuelle.
        var grouped = deliveredLines
            .GroupBy(l => new { l.ProductId, l.UnitPriceHT, l.DiscountPercent, l.FodecRatePercent })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.UnitPriceHT,
                g.Key.DiscountPercent,
                g.Key.FodecRatePercent,
                Product = g.First().Product,
                Quantity = g.Sum(l => l.InvoiceableQuantity),
                Designation = g.First().Designation
            })
            .OrderBy(g => g.Designation)
            .ToList();

        foreach (var line in grouped)
        {
            if (line.Product is null)
                return Result.Failure<Guid>(Error.NotFound("Produit", line.ProductId));

            var addResult = invoice.AddLine(
                line.Product,
                line.Quantity,
                Money.Create(line.UnitPriceHT),
                line.DiscountPercent,
                line.FodecRatePercent);

            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        // Un seul timbre pour la facture groupée, non un par bon.
        var stamp = await _fiscalStampResolver.ResolveSignedStampAsync(isCreditNote: false, cancellationToken);
        var stampResult = invoice.SetFiscalStampAmount(stamp);
        if (stampResult.IsFailure)
            return Result.Failure<Guid>(stampResult.Error);

        invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _invoiceRepository.AddAsync(invoice, cancellationToken);

        foreach (var note in orderedNotes)
        {
            var mark = note.MarkAsInvoiced(invoice);
            if (mark.IsFailure)
                return Result.Failure<Guid>(mark.Error);

            await _deliveryNoteRepository.UpdateAsync(note, cancellationToken);
        }

        try
        {
            await _auditService.LogAsync(
                AuditActions.Invoice.Created,
                "Invoice",
                invoice.Id,
                newValues: new
                {
                    invoice.Number.Value,
                    invoice.TotalAmount.Amount,
                    DeliveryNoteCount = orderedNotes.Count,
                    DeliveryNotes = orderedNotes.Select(n => n.Number.Value).ToList()
                },
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Audit failure must not roll back a successful invoice generation.
        }

        return Result.Success(invoice.Id);
    }
}