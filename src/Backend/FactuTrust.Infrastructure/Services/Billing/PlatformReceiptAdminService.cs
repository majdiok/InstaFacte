using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C4 — Service admin de gestion des reçus / encaissements plateforme.
///
/// Crée un reçu lié à une facture émise. Plusieurs reçus partiels possibles ;
/// la facture passe automatiquement à <c>Paid</c> si la somme des reçus
/// confirmés atteint le TotalTTC, sinon à <c>PartiallyPaid</c>.
/// </summary>
public sealed class PlatformReceiptAdminService : IPlatformReceiptAdminService
{
    private readonly MasterDbContext _db;
    private readonly IPlatformDocumentNumberService _numberService;
    private readonly IPlatformFiscalSettingsService _fiscalService;
    private readonly IPlatformInvoicePdfRenderer _pdfRenderer;

    public PlatformReceiptAdminService(
        MasterDbContext db,
        IPlatformDocumentNumberService numberService,
        IPlatformFiscalSettingsService fiscalService,
        IPlatformInvoicePdfRenderer pdfRenderer)
    {
        _db = db;
        _numberService = numberService;
        _fiscalService = fiscalService;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<Result<PlatformReceiptDto>> CreateAsync(
        Guid invoiceId,
        CreatePlatformReceiptRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return Result.Failure<PlatformReceiptDto>(Error.Validation("Request", "Requête invalide"));

        var invoice = await _db.PlatformInvoices
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        if (invoice is null)
            return Result.Failure<PlatformReceiptDto>(Error.NotFound(nameof(PlatformInvoice), invoiceId));

        if (invoice.Status is PlatformInvoiceStatus.Draft or PlatformInvoiceStatus.Cancelled or PlatformInvoiceStatus.Refunded)
            return Result.Failure<PlatformReceiptDto>(Error.Conflict($"Reçu impossible : facture {invoice.Status.ToDisplayString()}."));

        var fiscal = await _fiscalService.GetAsync(cancellationToken);
        var (number, year) = await _numberService.ReserveAsync("Receipt", fiscal.ReceiptNumberPrefix, cancellationToken);

        PlatformReceipt receipt;
        try
        {
            receipt = PlatformReceipt.Create(
                invoiceId: invoice.Id,
                receiptNumber: number,
                sequenceYear: year,
                paymentDate: request.PaymentDate ?? DateTime.UtcNow,
                method: request.Method,
                amountTND: request.AmountTND,
                receivedByUserId: actorUserId,
                reference: request.Reference,
                providerTxId: request.ProviderTxId,
                autoConfirm: request.AutoConfirm);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PlatformReceiptDto>(Error.Validation("Receipt", ex.Message));
        }

        invoice.AddReceipt(receipt);
        receipt.SetAuditInfo(actorUserId.ToString());

        // Met à jour le statut de la facture en fonction des reçus confirmés.
        if (receipt.Status == PlatformReceiptStatus.Confirmed)
        {
            UpdateInvoiceStatusAfterReceiptChange(invoice);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(Map(receipt));
    }

    public async Task<Result> CancelAsync(
        Guid receiptId,
        CancelPlatformReceiptRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null) return Result.Failure(Error.Validation("Request", "Requête invalide"));

        var receipt = await _db.PlatformReceipts.FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);
        if (receipt is null) return Result.Failure(Error.NotFound(nameof(PlatformReceipt), receiptId));
        if (receipt.Status == PlatformReceiptStatus.Cancelled) return Result.Success();

        var invoice = await _db.PlatformInvoices
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == receipt.InvoiceId, cancellationToken);
        if (invoice is null) return Result.Failure(Error.NotFound(nameof(PlatformInvoice), receipt.InvoiceId));

        receipt.Cancel(request.Reason);
        receipt.SetAuditInfo(actorUserId.ToString(), isUpdate: true);

        UpdateInvoiceStatusAfterReceiptChange(invoice);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ConfirmAsync(Guid receiptId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var receipt = await _db.PlatformReceipts.FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);
        if (receipt is null) return Result.Failure(Error.NotFound(nameof(PlatformReceipt), receiptId));
        if (receipt.Status == PlatformReceiptStatus.Confirmed) return Result.Success();
        if (receipt.Status == PlatformReceiptStatus.Cancelled)
            return Result.Failure(Error.Conflict("Reçu annulé non re-confirmable."));

        var invoice = await _db.PlatformInvoices
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == receipt.InvoiceId, cancellationToken);
        if (invoice is null) return Result.Failure(Error.NotFound(nameof(PlatformInvoice), receipt.InvoiceId));

        receipt.Confirm();
        receipt.SetAuditInfo(actorUserId.ToString(), isUpdate: true);

        UpdateInvoiceStatusAfterReceiptChange(invoice);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Lot C4 (complément) — Rend le PDF d'un reçu (avec l'entête de la facture parente).
    /// </summary>
    public async Task<Result<byte[]>> GetPdfAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await _db.PlatformReceipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);
        if (receipt is null)
            return Result.Failure<byte[]>(Error.NotFound(nameof(PlatformReceipt), receiptId));

        var invoice = await _db.PlatformInvoices
            .Include(i => i.Lines)
            .Include(i => i.Receipts)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == receipt.InvoiceId, cancellationToken);
        if (invoice is null)
            return Result.Failure<byte[]>(Error.NotFound(nameof(PlatformInvoice), receipt.InvoiceId));

        var fiscal = await _fiscalService.GetAsync(cancellationToken);

        // Construction du DTO parent (sans tout BuildDetailDtoAsync — on charge minimalement le tenant).
        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == invoice.TenantId)
            .Select(t => new { t.CompanyName, NifValue = t.NIF.Value })
            .FirstOrDefaultAsync(cancellationToken);

        var parentDto = new PlatformInvoiceDetailDto
        {
            Id = invoice.Id,
            TenantId = invoice.TenantId,
            TenantName = tenant?.CompanyName ?? "—",
            TenantNif = tenant?.NifValue,
            Number = invoice.Number,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            BillingType = invoice.BillingType,
            BillingTypeDisplay = invoice.BillingType.ToDisplayString(),
            Status = invoice.Status,
            StatusDisplay = invoice.Status.ToDisplayString(),
            SubtotalHT = invoice.SubtotalHT,
            VatAmount = invoice.VatAmount,
            DiscountAmount = invoice.DiscountAmount,
            CreditsApplied = invoice.CreditsApplied,
            StampDuty = invoice.StampDuty,
            TotalTTC = invoice.TotalTTC,
            TotalReceived = invoice.TotalReceived(),
            RemainingAmount = Math.Max(0m, invoice.TotalTTC - invoice.TotalReceived()),
            CreatedAt = invoice.CreatedAt,
            Lines = Array.Empty<PlatformInvoiceLineDto>(),
            Receipts = Array.Empty<PlatformReceiptDto>()
        };

        var receiptDto = Map(receipt);
        var bytes = _pdfRenderer.RenderReceipt(receiptDto, parentDto, fiscal);
        return Result.Success(bytes);
    }

    private static void UpdateInvoiceStatusAfterReceiptChange(PlatformInvoice invoice)
    {
        var totalReceived = invoice.TotalReceived();
        if (totalReceived <= 0m)
        {
            // Aucun paiement confirmé : reset à Issued / Overdue selon DueDate.
            if (invoice.Status is PlatformInvoiceStatus.Paid or PlatformInvoiceStatus.PartiallyPaid)
            {
                if (invoice.DueDate.HasValue && invoice.DueDate.Value < DateTime.UtcNow)
                    invoice.MarkOverdue();
                // Sinon, on laisse le statut courant (Issued).
            }
            return;
        }

        if (totalReceived >= invoice.TotalTTC)
        {
            invoice.MarkPaid();
        }
        else
        {
            invoice.MarkPartiallyPaid();
        }
    }

    private static PlatformReceiptDto Map(PlatformReceipt r) => new()
    {
        Id = r.Id,
        InvoiceId = r.InvoiceId,
        ReceiptNumber = r.ReceiptNumber,
        PaymentDate = r.PaymentDate,
        Method = r.Method,
        MethodDisplay = r.Method.ToDisplayString(),
        Status = r.Status,
        StatusDisplay = r.Status.ToDisplayString(),
        Reference = r.Reference,
        AmountTND = r.AmountTND,
        ProviderTxId = r.ProviderTxId,
        ConfirmedAt = r.ConfirmedAt,
        CancelledAt = r.CancelledAt,
        CancelledReason = r.CancelledReason,
        CreatedAt = r.CreatedAt
    };
}
