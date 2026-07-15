using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Commands;

/// <summary>
/// Command to accept a quote.
/// </summary>
public sealed record AcceptQuoteCommand(Guid QuoteId) : IRequest<Result>;

/// <summary>
/// Handler for AcceptQuoteCommand.
/// </summary>
public sealed class AcceptQuoteCommandHandler : IRequestHandler<AcceptQuoteCommand, Result>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public AcceptQuoteCommandHandler(
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

    public async Task<Result> Handle(AcceptQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdWithLinesAsync(request.QuoteId, cancellationToken);
        
        if (quote is null)
            return Result.Failure(Error.NotFound("Devis", request.QuoteId));

        var result = quote.Accept();
        
        if (result.IsFailure)
            return result;

        quote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _quoteRepository.UpdateAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Quote.Accepted,
            "Quote",
            quote.Id,
            oldValues: new { Status = "Sent", QuoteNumber = quote.Number.Value },
            newValues: new { Status = "Accepted", QuoteNumber = quote.Number.Value, AcceptedAt = quote.AcceptedAt },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
