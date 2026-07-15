using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Commands;

/// <summary>
/// Command to send a quote to the client.
/// </summary>
public sealed record SendQuoteCommand(Guid QuoteId) : IRequest<Result>;

/// <summary>
/// Handler for SendQuoteCommand.
/// </summary>
public sealed class SendQuoteCommandHandler : IRequestHandler<SendQuoteCommand, Result>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public SendQuoteCommandHandler(
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

    public async Task<Result> Handle(SendQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdWithLinesAsync(request.QuoteId, cancellationToken);
        
        if (quote is null)
            return Result.Failure(Error.NotFound("Devis", request.QuoteId));

        var result = quote.Send();
        
        if (result.IsFailure)
            return result;

        quote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _quoteRepository.UpdateAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Quote.Sent,
            "Quote",
            quote.Id,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
