namespace FactuTrust.Application.DTOs;

public sealed record ValidateInvoiceRequest(
    IReadOnlyList<DocumentLineAllocationsDto>? LineAllocations = null);

public sealed record RecordDeliveryWithAllocationsRequest(
    RecordDeliveryDto Delivery,
    IReadOnlyList<DocumentLineAllocationsDto>? LineAllocations = null);
