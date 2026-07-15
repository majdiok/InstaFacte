using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Lot C4 — Service admin facturation plateforme (CRUD + cycle).</summary>
public interface IPlatformInvoiceAdminService
{
    Task<PlatformInvoicesPageDto> ListAsync(
        Guid? tenantId,
        string? statusFilter,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformInvoiceDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PlatformInvoiceDetailDto>> CreateDraftAsync(CreatePlatformInvoiceRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<PlatformInvoiceDetailDto>> IssueAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PlatformInvoiceDetailDto>> CancelAsync(Guid id, CancelPlatformInvoiceRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Récupère le PDF (octets) — re-rend si absent.</summary>
    Task<Result<byte[]>> GetPdfAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lot C4 (complément) — Liste paginée des factures d'un tenant (utilisée côté tenant pour son espace abonnement).</summary>
    Task<PlatformInvoicesPageDto> ListForTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Lot C4 (complément) — Émet un avoir partiel sans annuler la facture d'origine.</summary>
    Task<Result<PlatformInvoiceDetailDto>> IssueCreditNoteAsync(Guid id, IssueCreditNoteRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Lot C4 (complément) — Agrégation TVA par mois sur une année donnée (Plateforme — DGI).</summary>
    Task<IReadOnlyList<PlatformVatPeriodDto>> GetVatPeriodsAsync(int year, CancellationToken cancellationToken = default);
}

/// <summary>Lot C4 — Service admin reçus plateforme (paiements rattachés).</summary>
public interface IPlatformReceiptAdminService
{
    Task<Result<PlatformReceiptDto>> CreateAsync(Guid invoiceId, CreatePlatformReceiptRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(Guid receiptId, CancelPlatformReceiptRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result> ConfirmAsync(Guid receiptId, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Lot C4 (complément) — Rend le PDF d'un reçu (utilise le parent invoice pour entête).</summary>
    Task<Result<byte[]>> GetPdfAsync(Guid receiptId, CancellationToken cancellationToken = default);
}

/// <summary>Lot C4 — Lecture / mise à jour des paramètres fiscaux singleton.</summary>
public interface IPlatformFiscalSettingsService
{
    Task<PlatformFiscalSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<Result<PlatformFiscalSettingsDto>> UpdateAsync(UpdatePlatformFiscalSettingsRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Lot C4 — Réservation atomique d'un numéro de document fiscal.</summary>
public interface IPlatformDocumentNumberService
{
    /// <summary>Type de document = "Invoice" | "Receipt" | "CreditNote".</summary>
    Task<(string Number, int Year)> ReserveAsync(string documentType, string prefix, CancellationToken cancellationToken = default);
}

/// <summary>Lot C4 — Rendu PDF de facture / reçu plateforme via QuestPDF.</summary>
public interface IPlatformInvoicePdfRenderer
{
    /// <summary>Rend le PDF d'une facture émise — retourne les octets.</summary>
    byte[] RenderInvoice(PlatformInvoiceDetailDto invoice, PlatformFiscalSettingsDto fiscal);

    /// <summary>Rend le PDF d'un reçu (paiement encaissé).</summary>
    byte[] RenderReceipt(PlatformReceiptDto receipt, PlatformInvoiceDetailDto parent, PlatformFiscalSettingsDto fiscal);
}
