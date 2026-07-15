using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Services;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Commands;

/// <summary>
/// Command to duplicate a quote. Creates a new draft with a new number, same client and lines.
/// </summary>
public sealed record DuplicateQuoteCommand(Guid QuoteId) : IRequest<Result<Guid>>;

/// <summary>
/// Handler for DuplicateQuoteCommand.
/// </summary>
public sealed class DuplicateQuoteCommandHandler : IRequestHandler<DuplicateQuoteCommand, Result<Guid>>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IQuoteNumberGenerator _quoteNumberGenerator;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;

    public DuplicateQuoteCommandHandler(
        IQuoteRepository quoteRepository,
        IQuoteNumberGenerator quoteNumberGenerator,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService,
        ITenantContext tenantContext)
    {
        _quoteRepository = quoteRepository;
        _quoteNumberGenerator = quoteNumberGenerator;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
        _tenantContext = tenantContext;
    }

    public async Task<Result<Guid>> Handle(DuplicateQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdWithLinesAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result.Failure<Guid>(Error.NotFound("Devis", request.QuoteId));

        if (!quote.Lines.Any())
            return Result.Failure<Guid>(Error.Validation("Lines", "Le devis à dupliquer doit contenir au moins une ligne"));

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible.");
        var issueDate = DateTime.UtcNow.Date;
        var expiryDate = issueDate.AddDays(30);
        var quoteNumber = await _quoteNumberGenerator.ReserveNextNumberAsync(tenantId, "DEV", issueDate.Year, cancellationToken);

        var newQuoteResult = Quote.Create(
            quoteNumber,
            quote.Client,
            issueDate,
            expiryDate,
            quote.Reference != null ? $"{quote.Reference} (copie)" : null,
            quote.Notes,
            quote.TermsAndConditions);

        if (newQuoteResult.IsFailure)
            return Result.Failure<Guid>(newQuoteResult.Error);

        var newQuote = newQuoteResult.Value;

        foreach (var line in quote.Lines.OrderBy(l => l.LineNumber))
        {
            Result addResult;
            if (line.ProductId != Guid.Empty)
            {
                var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
                if (product is null)
                {
                    addResult = newQuote.AddCustomLine(
                        line.ProductName,
                        line.ProductDescription,
                        line.Quantity,
                        line.Unit ?? "unité",
                        line.UnitPrice,
                        line.VatRate,
                        line.DiscountPercent);
                }
                else
                {
                    addResult = newQuote.AddLine(product, line.Quantity, line.UnitPrice, line.DiscountPercent);
                }
            }
            else
            {
                addResult = newQuote.AddCustomLine(
                    line.ProductName,
                    line.ProductDescription,
                    line.Quantity,
                    line.Unit ?? "unité",
                    line.UnitPrice,
                    line.VatRate,
                    line.DiscountPercent);
            }

            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        foreach (var mention in quote.LegalMentions)
            newQuote.AddLegalMention(mention);

        newQuote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _quoteRepository.AddAsync(newQuote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Quote.Created,
            "Quote",
            newQuote.Id,
            newValues: new { newQuote.Number.Value, SourceQuoteId = quote.Id },
            cancellationToken: cancellationToken);

        return Result.Success(newQuote.Id);
    }
}
