using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SupplierInvoices.Queries;

/// <summary>
/// Query to get all payments for a supplier invoice.
/// </summary>
public sealed record GetSupplierPaymentsQuery(Guid SupplierInvoiceId) : IRequest<Result<IReadOnlyList<SupplierPaymentDto>>>;

/// <summary>
/// Handler for GetSupplierPaymentsQuery.
/// </summary>
public sealed class GetSupplierPaymentsQueryHandler : IRequestHandler<GetSupplierPaymentsQuery, Result<IReadOnlyList<SupplierPaymentDto>>>
{
    private readonly ISupplierPaymentRepository _repository;

    public GetSupplierPaymentsQueryHandler(ISupplierPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<SupplierPaymentDto>>> Handle(GetSupplierPaymentsQuery request, CancellationToken cancellationToken)
    {
        var payments = await _repository.GetBySupplierInvoiceIdAsync(request.SupplierInvoiceId, cancellationToken);

        var dtos = payments.Select(p => new SupplierPaymentDto
        {
            Id = p.Id,
            Amount = p.Amount.Amount,
            Currency = p.Amount.Currency,
            PaymentDate = p.PaymentDate,
            Method = (int)p.Method,
            MethodDisplay = p.Method.ToDisplayString(),
            Reference = p.Reference,
            Notes = p.Notes,
            CreatedAt = p.CreatedAt,
            EffetDueDate = p.EffetDueDate,
            EffetStatus = (int?)p.EffetStatus,
            EffetStatusDisplay = p.EffetStatus?.ToDisplayString()
        }).ToList();

        return Result.Success<IReadOnlyList<SupplierPaymentDto>>(dtos);
    }
}
