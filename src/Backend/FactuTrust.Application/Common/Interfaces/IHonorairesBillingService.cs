using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

public interface IHonorairesBillingService
{
    Task<Result<Guid>> CreateInvoiceDraftAsync(UpsertHonorairesInvoiceDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateInvoiceDraftAsync(Guid id, UpsertHonorairesInvoiceDto dto, CancellationToken cancellationToken = default);
    Task<Result> ValidateInvoiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> CancelInvoiceAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
    Task<Result<Guid>> CreateCreditNoteAsync(CreateHonorairesCreditNoteDto dto, CancellationToken cancellationToken = default);
    Task<HonorairesInvoiceDto?> GetInvoiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HonorairesPagedResult<HonorairesInvoiceListItemDto>> ListInvoicesAsync(
        HonorairesDocumentType? type, HonorairesInvoiceStatus? status, Guid? assignmentId, string? search,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<string> PreviewInvoiceNumberAsync(HonorairesDocumentType type, DateTime referenceDate, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportInvoicePdfAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateQuoteDraftAsync(UpsertHonorairesQuoteDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateQuoteDraftAsync(Guid id, UpsertHonorairesQuoteDto dto, CancellationToken cancellationToken = default);
    Task<Result> SendQuoteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> AcceptQuoteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> RejectQuoteAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
    Task<Result> CancelQuoteAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
    Task<Result<Guid>> ConvertQuoteToInvoiceAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<HonorairesQuoteDto?> GetQuoteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HonorairesPagedResult<HonorairesQuoteListItemDto>> ListQuotesAsync(
        HonorairesQuoteStatus? status, Guid? assignmentId, string? search,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<string> PreviewQuoteNumberAsync(DateTime referenceDate, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportQuotePdfAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<Guid>> RecordPaymentAsync(Guid invoiceId, RecordHonorairesPaymentDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HonorairesPaymentDto>> ListPaymentsAsync(Guid? invoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillableDossierDto>> ListBillableDossiersAsync(CancellationToken cancellationToken = default);

    Task<Result<Guid>> AddAttachmentAsync(
        HonorairesAttachmentDocumentKind kind, Guid documentId,
        string fileName, string contentType, Stream content, long sizeBytes,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HonorairesAttachmentListItemDto>> ListAttachmentsAsync(
        HonorairesAttachmentDocumentKind kind, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}

public sealed record HonorairesAttachmentListItemDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long SizeBytes { get; init; }
    public DateTime UploadedAt { get; init; }
}
