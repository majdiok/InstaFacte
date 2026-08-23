using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record CashRegisterDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public Guid WarehouseId { get; init; }
    public bool IsActive { get; init; }
    public bool RequireOpenSession { get; init; }
}

public sealed record CashRegisterSessionDto
{
    public Guid Id { get; init; }
    public Guid CashRegisterId { get; init; }
    public string CashRegisterCode { get; init; } = null!;
    public string CashRegisterName { get; init; } = null!;
    public Guid WarehouseId { get; init; }
    public CashRegisterSessionStatus Status { get; init; }
    public DateTime OpenedAt { get; init; }
    public Guid OpenedByUserId { get; init; }
    public decimal OpeningFloat { get; init; }
    public DateTime? ClosedAt { get; init; }
    public Guid? ZReportId { get; init; }
    public string? ZReportNumber { get; init; }
}

public sealed record OpenCashRegisterSessionRequest
{
    public Guid WarehouseId { get; init; }
    public decimal OpeningFloat { get; init; }
}

public sealed record CloseCashRegisterSessionRequest
{
    public decimal CountedCash { get; init; }
    public string? Notes { get; init; }
}

public sealed record PosCartStateDto
{
    public object? State { get; init; }
}

public sealed record SavePosCartRequest
{
    public Guid? WarehouseId { get; init; }
    public System.Text.Json.JsonElement State { get; init; }
}

public sealed record PosHeldTicketDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = null!;
    public decimal TotalTtc { get; init; }
    public int LineCount { get; init; }
    public DateTime HeldAt { get; init; }
    public object? State { get; init; }
}

public sealed record SavePosHeldTicketRequest
{
    public Guid? WarehouseId { get; init; }
    public Guid? Id { get; init; }
    public string Label { get; init; } = null!;
    public decimal TotalTtc { get; init; }
    public int LineCount { get; init; }
    public System.Text.Json.JsonElement State { get; init; }
}

public sealed record ImportPosHeldTicketsRequest
{
    public Guid WarehouseId { get; init; }
    public IReadOnlyList<SavePosHeldTicketRequest> Tickets { get; init; } = Array.Empty<SavePosHeldTicketRequest>();
}

public sealed record PosZTotalsByMethodDto
{
    public PaymentMethod Method { get; init; }
    public string MethodDisplay { get; init; } = null!;
    public decimal Amount { get; init; }
}

public sealed record PosSessionReportDto
{
    public Guid SessionId { get; init; }
    public Guid CashRegisterId { get; init; }
    public string CashRegisterName { get; init; } = null!;
    public DateTime OpenedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public decimal OpeningFloat { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal? CountedCash { get; init; }
    public decimal? CashVariance { get; init; }
    public int InvoiceCount { get; init; }
    public int CreditNoteCount { get; init; }
    public int HeldTicketCount { get; init; }
    public IReadOnlyList<PosZTotalsByMethodDto> TotalsByMethod { get; init; } = Array.Empty<PosZTotalsByMethodDto>();
    public IReadOnlyList<Guid> InvoiceIds { get; init; } = Array.Empty<Guid>();
    public string? ZReportNumber { get; init; }
    public string? Notes { get; init; }
}

public sealed record ZReportListItemDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = null!;
    public Guid CashRegisterSessionId { get; init; }
    public Guid CashRegisterId { get; init; }
    public string CashRegisterName { get; init; } = null!;
    public DateTime GeneratedAt { get; init; }
    public decimal OpeningFloat { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal CountedCash { get; init; }
    public decimal CashVariance { get; init; }
}
