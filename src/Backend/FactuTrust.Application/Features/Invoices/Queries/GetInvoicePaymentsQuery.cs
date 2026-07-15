using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Invoices.Queries;

/// <summary>
/// Query to get all payments for an invoice.
/// </summary>
public sealed record GetInvoicePaymentsQuery(Guid InvoiceId) : IRequest<Result<IReadOnlyList<PaymentDto>>>;

/// <summary>
/// Handler for GetInvoicePaymentsQuery.
/// </summary>
public sealed class GetInvoicePaymentsQueryHandler : IRequestHandler<GetInvoicePaymentsQuery, Result<IReadOnlyList<PaymentDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentRepository _paymentRepository;

    public GetInvoicePaymentsQueryHandler(IInvoiceRepository invoiceRepository, IPaymentRepository paymentRepository)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<PaymentDto>>> Handle(GetInvoicePaymentsQuery request, CancellationToken cancellationToken)
    {
        var invoiceExists = await _invoiceRepository.ExistsAsync(request.InvoiceId, cancellationToken);
        if (!invoiceExists)
            return Result.Failure<IReadOnlyList<PaymentDto>>(Error.NotFound("Facture", request.InvoiceId));

        var payments = await _paymentRepository.GetByInvoiceIdAsync(request.InvoiceId, cancellationToken);

        var dtos = payments.Select(p => new PaymentDto
        {
            Id = p.Id,
            Amount = p.Amount.Amount,
            Currency = p.Amount.Currency,
            PaymentDate = p.PaymentDate,
            Method = (int)p.Method,
            MethodDisplay = p.Method.ToDisplayString(),
            Reference = p.Reference,
            Notes = p.Notes,
            IsRefunded = p.IsRefunded,
            CreatedAt = p.CreatedAt,
            ClientWithholdingAmount = p.ClientWithholdingAmount,
            TotalAppliedTowardInvoice = p.GetTotalAppliedTowardInvoice(),
            EffetDueDate = p.EffetDueDate,
            EffetStatus = (int?)p.EffetStatus,
            EffetStatusDisplay = p.EffetStatus?.ToDisplayString()
        }).ToList();

        return Result.Success<IReadOnlyList<PaymentDto>>(dtos);
    }
}
