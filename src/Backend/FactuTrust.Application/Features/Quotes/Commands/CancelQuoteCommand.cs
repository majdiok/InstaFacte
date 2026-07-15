using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Commands;

/// <summary>
/// Command to cancel a quote.
/// </summary>
public sealed record CancelQuoteCommand(Guid QuoteId, string Reason) : IRequest<Result>;

/// <summary>
/// Handler for CancelQuoteCommand.
/// </summary>
public sealed class CancelQuoteCommandHandler : IRequestHandler<CancelQuoteCommand, Result>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public CancelQuoteCommandHandler(
        IQuoteRepository quoteRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(CancelQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdWithLinesAsync(request.QuoteId, cancellationToken);
        
        if (quote is null)
            return Result.Failure(Error.NotFound("Devis", request.QuoteId));

        var previousStatus = quote.Status;
        var quoteNumber = quote.Number.Value;

        var result = quote.Cancel(request.Reason);
        
        if (result.IsFailure)
            return result;

        quote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _quoteRepository.UpdateAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Quote.Cancelled,
            "Quote",
            quote.Id,
            oldValues: new { Status = previousStatus.ToString(), QuoteNumber = quoteNumber },
            newValues: new { Status = "Cancelled", QuoteNumber = quoteNumber, CancelledAt = quote.CancelledAt, Reason = request.Reason },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
