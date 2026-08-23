using FactuTrust.Application.Common.Interfaces.Services;

namespace FactuTrust.Application.DTOs;

/// <summary>Line-level lot/serial allocations for document validate/deliver endpoints.</summary>
public sealed record DocumentLineAllocationsDto(
    Guid LineId,
    IReadOnlyList<StockAllocationInput> Allocations);
