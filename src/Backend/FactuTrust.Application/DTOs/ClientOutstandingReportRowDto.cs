namespace FactuTrust.Application.DTOs;

/// <summary>Une ligne de l'état encours commercial multi-clients (Studio IA).</summary>
public sealed record ClientOutstandingReportRowDto
{
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public decimal UnpaidInvoicesAmount { get; init; }
    public decimal ConfirmedOrdersAmount { get; init; }
    public decimal TotalOutstanding { get; init; }
    public decimal? CreditLimit { get; init; }
    public decimal? AvailableCredit { get; init; }
    public bool IsOverLimit { get; init; }
    public int UnpaidInvoiceCount { get; init; }
    public decimal OverdueAmount { get; init; }
    public string Currency { get; init; } = "TND";
}
