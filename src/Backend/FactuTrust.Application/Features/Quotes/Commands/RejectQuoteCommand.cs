using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Commands;

/// <summary>
/// Command to reject a quote.
/// </summary>
public sealed record RejectQuoteCommand(Guid QuoteId, string? Reason = null) : IRequest<Result>;

/// <summary>
/// Handler for RejectQuoteCommand.
/// </summary>
public sealed class RejectQuoteCommandHandler : IRequestHandler<RejectQuoteCommand, Result>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public RejectQuoteCommandHandler(
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

    public async Task<Result> Handle(RejectQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdWithLinesAsync(request.QuoteId, cancellationToken);
        
        if (quote is null)
            return Result.Failure(Error.NotFound("Devis", request.QuoteId));

        var result = quote.Reject(request.Reason);
        
        if (result.IsFailure)
            return result;

        quote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _quoteRepository.UpdateAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Quote.Rejected,
            "Quote",
            quote.Id,
            oldValues: new { Status = "Sent", QuoteNumber = quote.Number.Value },
            newValues: new { Status = "Rejected", QuoteNumber = quote.Number.Value, RejectedAt = quote.RejectedAt, Reason = request.Reason },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
