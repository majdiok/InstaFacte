using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.Pricing.Commands;

/// <summary>
/// Pose (ou retire) la remise de pied d'un document. Pourcentage et montant sont exclusifs ;
/// les deux à <c>null</c> retirent la remise.
///
/// La remise est répartie par le domaine sur les lignes, au prorata de leur base HT, de sorte
/// que le FODEC et la base de TVA portent sur ce qui est réellement facturé.
/// </summary>
public sealed record SetQuoteGlobalDiscountCommand(
    Guid QuoteId, decimal? Percent, decimal? Amount) : IRequest<Result>;

public sealed class SetQuoteGlobalDiscountCommandHandler
    : IRequestHandler<SetQuoteGlobalDiscountCommand, Result>
{
    private readonly IQuoteRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public SetQuoteGlobalDiscountCommandHandler(
        IQuoteRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(SetQuoteGlobalDiscountCommand request, CancellationToken cancellationToken)
    {
        var quote = await _repository.GetByIdWithLinesAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result.Failure(Error.NotFound("Devis", request.QuoteId));

        if (!quote.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Ce devis ne peut plus être modifié"));

        var result = quote.SetGlobalDiscount(request.Percent, ToMoney(request.Amount));
        if (result.IsFailure)
            return result;

        quote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _repository.UpdateAsync(quote, cancellationToken);

        await _auditService.LogAsync(
            "Quote.GlobalDiscountSet", "Quote", quote.Id,
            newValues: new { request.Percent, Applied = quote.GlobalDiscountAmount.Amount },
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    internal static Money? ToMoney(decimal? amount) =>
        amount is > 0 ? Money.Create(amount.Value) : null;
}

public sealed record SetSalesOrderGlobalDiscountCommand(
    Guid SalesOrderId, decimal? Percent, decimal? Amount) : IRequest<Result>;

public sealed class SetSalesOrderGlobalDiscountCommandHandler
    : IRequestHandler<SetSalesOrderGlobalDiscountCommand, Result>
{
    private readonly ISalesOrderRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public SetSalesOrderGlobalDiscountCommandHandler(
        ISalesOrderRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(SetSalesOrderGlobalDiscountCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdWithLinesAsync(request.SalesOrderId, cancellationToken);
        if (order is null)
            return Result.Failure(Error.NotFound("Commande", request.SalesOrderId));

        if (!order.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Cette commande ne peut plus être modifiée"));

        var result = order.SetGlobalDiscount(
            request.Percent, SetQuoteGlobalDiscountCommandHandler.ToMoney(request.Amount));
        if (result.IsFailure)
            return result;

        order.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _repository.UpdateAsync(order, cancellationToken);

        await _auditService.LogAsync(
            "SalesOrder.GlobalDiscountSet", "SalesOrder", order.Id,
            newValues: new { request.Percent, Applied = order.GlobalDiscountAmount.Amount },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record SetInvoiceGlobalDiscountCommand(
    Guid InvoiceId, decimal? Percent, decimal? Amount) : IRequest<Result>;

public sealed class SetInvoiceGlobalDiscountCommandHandler
    : IRequestHandler<SetInvoiceGlobalDiscountCommand, Result>
{
    private readonly IInvoiceRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public SetInvoiceGlobalDiscountCommandHandler(
        IInvoiceRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(SetInvoiceGlobalDiscountCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _repository.GetByIdWithLinesAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return Result.Failure(Error.NotFound("Facture", request.InvoiceId));

        // Une facture validée ne se remise plus : on ne mute jamais un document émis.
        if (invoice.Status != Domain.Enums.InvoiceStatus.Draft)
            return Result.Failure(Error.Validation("Status", "Seule une facture en brouillon peut être remisée"));

        var result = invoice.SetGlobalDiscount(
            request.Percent, SetQuoteGlobalDiscountCommandHandler.ToMoney(request.Amount));
        if (result.IsFailure)
            return result;

        invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _repository.UpdateAsync(invoice, cancellationToken);

        await _auditService.LogAsync(
            "Invoice.GlobalDiscountSet", "Invoice", invoice.Id,
            newValues: new { request.Percent, Applied = invoice.GlobalDiscountAmount.Amount },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
