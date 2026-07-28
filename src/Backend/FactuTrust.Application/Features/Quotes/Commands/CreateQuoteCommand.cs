using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Commands;

/// <summary>
/// Command to create a new quote.
/// </summary>
public sealed record CreateQuoteCommand(CreateQuoteDto Quote) : IRequest<Result<Guid>>;

/// <summary>
/// Validator for CreateQuoteCommand.
/// </summary>
public sealed class CreateQuoteCommandValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteCommandValidator()
    {
        RuleFor(x => x.Quote.ClientId)
            .NotEmpty()
            .WithMessage("Le client est obligatoire");

        RuleFor(x => x.Quote.IssueDate)
            .NotEmpty()
            .WithMessage("La date d'émission est obligatoire");

        RuleFor(x => x.Quote.ExpiryDate)
            .GreaterThan(x => x.Quote.IssueDate)
            .WithMessage("La date d'expiration doit être postérieure à la date d'émission");

        RuleFor(x => x.Quote.Lines)
            .NotEmpty()
            .WithMessage("Le devis doit contenir au moins une ligne");

        RuleForEach(x => x.Quote.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .WithMessage("La quantité doit être supérieure à zéro");

            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Le prix unitaire doit être positif");

            line.RuleFor(l => l.DiscountPercent)
                .InclusiveBetween(0, 100)
                .When(l => l.DiscountPercent.HasValue)
                .WithMessage("La remise doit être comprise entre 0% et 100%");

            line.RuleFor(l => l)
                .Must(l => l.ProductId.HasValue || !string.IsNullOrWhiteSpace(l.Designation))
                .WithMessage("Soit un produit doit être sélectionné, soit une désignation doit être fournie");
        });
    }
}

/// <summary>
/// Handler for CreateQuoteCommand.
/// </summary>
public sealed class CreateQuoteCommandHandler : IRequestHandler<CreateQuoteCommand, Result<Guid>>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IProductRepository _productRepository;
    private readonly IQuoteTemplateRepository _quoteTemplateRepository;
    private readonly IQuoteNumberGenerator _quoteNumberGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;

    private readonly ISubscriptionResolver _subscriptionResolver;
    private readonly IFiscalStampResolver _fiscalStampResolver;

    public CreateQuoteCommandHandler(
        IQuoteRepository quoteRepository,
        IClientRepository clientRepository,
        IProductRepository productRepository,
        IQuoteTemplateRepository quoteTemplateRepository,
        IQuoteNumberGenerator quoteNumberGenerator,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService,
        ITenantContext tenantContext,
        ISubscriptionResolver subscriptionResolver,
        IFiscalStampResolver fiscalStampResolver)
    {
        _quoteRepository = quoteRepository;
        _clientRepository = clientRepository;
        _productRepository = productRepository;
        _quoteTemplateRepository = quoteTemplateRepository;
        _quoteNumberGenerator = quoteNumberGenerator;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
        _tenantContext = tenantContext;
        _subscriptionResolver = subscriptionResolver;
        _fiscalStampResolver = fiscalStampResolver;
    }

    public async Task<Result<Guid>> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Quote;

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible.");

        var plan = await _subscriptionResolver.GetPlanForTenantAsync(tenantId, cancellationToken);
        var limit = SubscriptionLimits.GetMaxQuotesPerMonth(plan);
        if (limit < int.MaxValue)
        {
            var now = DateTime.UtcNow;
            var count = await _quoteRepository.GetMonthlyCountAsync(now.Year, now.Month, cancellationToken);
            if (count >= limit)
                return Result.Failure<Guid>(Error.Validation("Quotes",
                    "Limite de devis atteinte pour ce mois. Passez à un forfait supérieur pour en créer davantage."));
        }

        // Get client
        var client = await _clientRepository.GetByIdAsync(dto.ClientId, cancellationToken);
        if (client is null)
            return Result.Failure<Guid>(Error.NotFound("Client", dto.ClientId));

        if (!client.IsActive)
            return Result.Failure<Guid>(Error.Validation("Client", "Ce client est désactivé"));

        if (dto.QuoteTemplateId.HasValue)
        {
            var tpl = await _quoteTemplateRepository.GetByIdAsync(dto.QuoteTemplateId.Value, cancellationToken);
            if (tpl is null)
                return Result.Failure<Guid>(Error.Validation("QuoteTemplateId", "Modèle de devis introuvable"));
            if (!tpl.IsActive)
                return Result.Failure<Guid>(Error.Validation("QuoteTemplateId", "Ce modèle de devis est désactivé"));
        }

        // Generate quote number
        var year = dto.IssueDate.Year;
        var quoteNumber = await _quoteNumberGenerator.ReserveNextNumberAsync(
            tenantId,
            "DEV",
            year,
            cancellationToken);

        // Create quote
        var quoteResult = Quote.Create(
            quoteNumber,
            client,
            dto.IssueDate,
            dto.ExpiryDate,
            dto.Reference,
            dto.Notes,
            dto.TermsAndConditions);

        if (quoteResult.IsFailure)
            return Result.Failure<Guid>(quoteResult.Error);

        var quote = quoteResult.Value;

        // Add lines
        foreach (var lineDto in dto.Lines)
        {
            Result addLineResult;

            if (lineDto.ProductId.HasValue)
            {
                // Use existing product
                var product = await _productRepository.GetByIdAsync(lineDto.ProductId.Value, cancellationToken);
                if (product is null)
                    return Result.Failure<Guid>(Error.NotFound("Produit", lineDto.ProductId.Value));

                if (!product.IsActive)
                    return Result.Failure<Guid>(Error.Validation("Produit", $"Le produit '{product.Name}' est désactivé"));

                if (lineDto.DiscountPercent.HasValue)
                {
                    if (lineDto.DiscountPercent.Value < 0 || lineDto.DiscountPercent.Value > 100)
                        return Result.Failure<Guid>(Error.Validation("DiscountPercent", "La remise doit être comprise entre 0% et 100%"));

                    if (product.IsDiscountEnabled && product.MaxDiscountPercent.HasValue &&
                        lineDto.DiscountPercent.Value > product.MaxDiscountPercent.Value)
                    {
                        return Result.Failure<Guid>(Error.Validation(
                            "DiscountPercent",
                            $"La remise ne peut pas dépasser {product.MaxDiscountPercent.Value}% pour le produit '{product.Name}'"));
                    }
                }

                Money? customPrice = lineDto.UnitPrice > 0 
                    ? Money.Create(lineDto.UnitPrice) 
                    : null;

                addLineResult = quote.AddLine(product, lineDto.Quantity, customPrice, lineDto.DiscountPercent);
            }
            else
            {
                // Custom line
                if (string.IsNullOrWhiteSpace(lineDto.Designation))
                    return Result.Failure<Guid>(Error.Validation("Designation", "La désignation est obligatoire pour une ligne personnalisée"));

                var vatRate = VatRateExtensions.FromPercent(lineDto.VatRatePercent);
                var unitPrice = Money.Create(lineDto.UnitPrice);

                addLineResult = quote.AddCustomLine(
                    lineDto.Designation,
                    lineDto.Description,
                    lineDto.Quantity,
                    lineDto.Unit ?? "unité",
                    unitPrice,
                    vatRate,
                    lineDto.DiscountPercent);
            }

            if (addLineResult.IsFailure)
                return Result.Failure<Guid>(addLineResult.Error);
        }

        // Timbre fiscal annoncé dès le devis, avec le même résolveur que la facture :
        // sans lui, la facture dépassait systématiquement le devis accepté.
        var stamp = await _fiscalStampResolver.ResolveSignedStampAsync(isCreditNote: false, cancellationToken);
        var stampResult = quote.SetFiscalStampAmount(stamp);
        if (stampResult.IsFailure)
            return Result.Failure<Guid>(stampResult.Error);

        // Set audit info
        quote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        // Save
        await _quoteRepository.AddAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Audit log
        await _auditService.LogAsync(
            AuditActions.Quote.Created,
            "Quote",
            quote.Id,
            newValues: new { quote.Number.Value, quote.TotalAmount.Amount },
            cancellationToken: cancellationToken);

        if (dto.QuoteTemplateId.HasValue)
            await _quoteTemplateRepository.IncrementUsageAsync(dto.QuoteTemplateId.Value, cancellationToken);

        return Result.Success(quote.Id);
    }
}
