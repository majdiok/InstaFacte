using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record PortalMeDto
{
    public string SellerName { get; init; } = null!;
    public string? SellerTradeName { get; init; }
    public string? SellerLogoUrl { get; init; }
    public string? SellerEmail { get; init; }
    public string? BankName { get; init; }
    public string? Iban { get; init; }
    public string? Rib { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public string? ClientEmail { get; init; }
    public string? ClientNif { get; init; }
    public string ContactEmail { get; init; } = null!;
    public string ContactName { get; init; } = null!;
}

public sealed record PortalSummaryDto
{
    public decimal UnpaidAmount { get; init; }
    public int UnpaidCount { get; init; }
    public decimal OverdueAmount { get; init; }
    public decimal TotalOutstanding { get; init; }
    public IReadOnlyList<PortalInvoiceListItemDto> UpcomingInvoices { get; init; } = [];
}

public sealed record PortalInvoiceListItemDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public string Type { get; init; } = null!;
    public bool IsCreditNote { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public InvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public decimal TotalAmount { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal RemainingAmount { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record PortalInvoiceDetailDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public string Type { get; init; } = null!;
    public bool IsCreditNote { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public InvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public decimal SubTotal { get; init; }
    public decimal TotalVat { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal RemainingAmount { get; init; }
    public string Currency { get; init; } = "TND";
    public IReadOnlyList<PortalInvoiceLineDto> Lines { get; init; } = [];
    public IReadOnlyList<PortalPaymentDto> Payments { get; init; } = [];
}

public sealed record PortalInvoiceLineDto
{
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public int VatRatePercent { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed record PortalPaymentDto
{
    public Guid Id { get; init; }
    public Guid InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    public DateTime PaymentDate { get; init; }
    public decimal Amount { get; init; }
    public string MethodDisplay { get; init; } = null!;
    public string? Reference { get; init; }
}

public sealed record PortalStatementLineDto
{
    public DateTime Date { get; init; }
    public string Kind { get; init; } = null!;
    public string Label { get; init; } = null!;
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal Balance { get; init; }
}

public sealed record PortalStatementDto
{
    public string ClientName { get; init; } = null!;
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal ClosingBalance { get; init; }
    public IReadOnlyList<PortalStatementLineDto> Lines { get; init; } = [];
}

public sealed record ClientPortalContactDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public ClientPortalContactStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public DateTime InvitedAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public DateTime? LastAccessAt { get; init; }
}

public sealed record InviteClientPortalContactRequest
{
    public string Email { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
}

public sealed record AcceptPortalInviteRequest
{
    public string Token { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string ConfirmPassword { get; init; } = null!;
}

public sealed record UpdateClientPortalSettingsRequest
{
    public bool Enabled { get; init; }
}
