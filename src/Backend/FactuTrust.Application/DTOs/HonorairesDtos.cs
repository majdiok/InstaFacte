using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record HonorairesLineWriteDto
{
    public Guid? Id { get; init; }
    /// <summary>Code activité cabinet (ex. TENUE). Null si le cabinet n'a pas de référentiel.</summary>
    public string? ActivityCode { get; init; }
    public string Designation { get; init; } = null!;
    public string? Description { get; init; }
    public decimal Quantity { get; init; } = 1;
    public decimal UnitPrice { get; init; }
    public int VatRate { get; init; } = 19;
    public decimal? DiscountPercent { get; init; }
}

public sealed record HonorairesLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public string? ActivityCode { get; init; }
    public string Designation { get; init; } = null!;
    public string? Description { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public int VatRate { get; init; }
    public decimal? DiscountPercent { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal SubTotal { get; init; }
    public decimal VatAmount { get; init; }
    public decimal Total { get; init; }
}

public sealed record UpsertHonorairesInvoiceDto
{
    public Guid FirmClientAssignmentId { get; init; }
    public string ClientName { get; init; } = null!;
    public string? ClientNif { get; init; }
    public string? ClientAddress { get; init; }
    public string? ContactName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public string Currency { get; init; } = "TND";
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public string? PaymentMethod { get; init; }
    public string? BankAccountLabel { get; init; }
    public decimal WithholdingAmount { get; init; }
    public bool IsRecurring { get; init; }
    public BillingFrequency? RecurrenceFrequency { get; init; }
    public Guid? SourceQuoteId { get; init; }
    public List<HonorairesLineWriteDto> Lines { get; init; } = new();
}

public sealed record CreateHonorairesCreditNoteDto
{
    public Guid LinkedInvoiceId { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Notes { get; init; }
    public List<HonorairesLineWriteDto>? Lines { get; init; }
}

public sealed record HonorairesInvoiceDto
{
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public HonorairesInvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public HonorairesDocumentType Type { get; init; }
    public bool IsCreditNote { get; init; }
    public Guid FirmClientAssignmentId { get; init; }
    public string ClientName { get; init; } = null!;
    public string? ClientNif { get; init; }
    public string? ClientAddress { get; init; }
    public string? ContactName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public string? PaymentMethod { get; init; }
    public string? BankAccountLabel { get; init; }
    public string Currency { get; init; } = "TND";
    public Guid? SourceQuoteId { get; init; }
    public Guid? LinkedInvoiceId { get; init; }
    /// <summary>Number of the source invoice when this document is a credit note.</summary>
    public string? LinkedInvoiceNumber { get; init; }
    public decimal SubTotal { get; init; }
    public decimal TotalVat { get; init; }
    public decimal WithholdingAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal AmountDue { get; init; }
    public bool IsRecurring { get; init; }
    public BillingFrequency? RecurrenceFrequency { get; init; }
    public List<HonorairesLineDto> Lines { get; init; } = new();
    public List<HonorairesPaymentDto> Payments { get; init; } = new();
}

public sealed record HonorairesInvoiceListItemDto
{
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public HonorairesInvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public HonorairesDocumentType Type { get; init; }
    /// <summary>Source invoice when this list item is a credit note (avoir).</summary>
    public Guid? LinkedInvoiceId { get; init; }
    public string ClientName { get; init; } = null!;
    public Guid FirmClientAssignmentId { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AmountDue { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record UpsertHonorairesQuoteDto
{
    public Guid FirmClientAssignmentId { get; init; }
    public string ClientName { get; init; } = null!;
    public string? ClientNif { get; init; }
    public string? ClientAddress { get; init; }
    public string? ContactName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string Currency { get; init; } = "TND";
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public List<HonorairesLineWriteDto> Lines { get; init; } = new();
}

public sealed record HonorairesQuoteDto
{
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public HonorairesQuoteStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public Guid FirmClientAssignmentId { get; init; }
    public string ClientName { get; init; } = null!;
    public string? ClientNif { get; init; }
    public string? ClientAddress { get; init; }
    public string? ContactName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public string Currency { get; init; } = "TND";
    public Guid? ConvertedInvoiceId { get; init; }
    public decimal SubTotal { get; init; }
    public decimal TotalVat { get; init; }
    public decimal TotalAmount { get; init; }
    public List<HonorairesLineDto> Lines { get; init; } = new();
}

public sealed record HonorairesQuoteListItemDto
{
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public DateTime IssueDate { get; init; }
    public HonorairesQuoteStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string ClientName { get; init; } = null!;
    public Guid FirmClientAssignmentId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record RecordHonorairesPaymentDto
{
    public DateTime PaymentDate { get; init; }
    public decimal Amount { get; init; }
    public decimal ClientWithholdingAmount { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? BankAccountLabel { get; init; }
}

public sealed record HonorairesPaymentDto
{
    public Guid Id { get; init; }
    public Guid HonorairesInvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    public string? ClientName { get; init; }
    public DateTime PaymentDate { get; init; }
    public decimal Amount { get; init; }
    public decimal ClientWithholdingAmount { get; init; }
    public decimal AppliedAmount { get; init; }
    public PaymentMethod Method { get; init; }
    public string MethodDisplay { get; init; } = null!;
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? BankAccountLabel { get; init; }
}

public sealed record BillableDossierDto
{
    public Guid AssignmentId { get; init; }
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public string? Nif { get; init; }
    public string? Address { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public decimal? AnnualFeeAmount { get; init; }
    public BillingFrequency? BillingFrequency { get; init; }
    public string? SuggestedLineDesignation { get; init; }
}

public sealed record HonorairesPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
