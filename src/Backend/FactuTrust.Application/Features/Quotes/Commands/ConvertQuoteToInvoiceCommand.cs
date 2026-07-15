using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Commands;

/// <summary>
/// Command to convert an accepted quote to an invoice.
/// Executed atomically via IQuoteToInvoiceConversionService.
/// </summary>
public sealed record ConvertQuoteToInvoiceCommand(
    Guid QuoteId,
    ConvertQuoteToInvoiceDto? Options = null) : IRequest<Result<Guid>>;

/// <summary>
/// Validator for ConvertQuoteToInvoiceCommand.
/// </summary>
public sealed class ConvertQuoteToInvoiceCommandValidator : AbstractValidator<ConvertQuoteToInvoiceCommand>
{
    public ConvertQuoteToInvoiceCommandValidator()
    {
        RuleFor(x => x.QuoteId)
            .NotEmpty()
            .WithMessage("L'identifiant du devis est obligatoire");

        When(x => x.Options != null, () =>
        {
            RuleFor(x => x.Options!.IssueDate)
                .GreaterThan(DateTime.MinValue)
                .When(x => x.Options!.IssueDate.HasValue)
                .WithMessage("La date d'émission doit être valide");

            RuleFor(x => x.Options!.DueDate)
                .GreaterThanOrEqualTo(x => x.Options!.IssueDate)
                .When(x => x.Options!.IssueDate.HasValue && x.Options!.DueDate.HasValue)
                .WithMessage("La date d'échéance doit être postérieure à la date d'émission");
        });
    }
}

/// <summary>
/// Handler for ConvertQuoteToInvoiceCommand.
/// Delegates to conversion service for atomic, secure processing.
/// </summary>
public sealed class ConvertQuoteToInvoiceCommandHandler : IRequestHandler<ConvertQuoteToInvoiceCommand, Result<Guid>>
{
    private readonly IQuoteToInvoiceConversionService _conversionService;
    private readonly ICurrentUser _currentUser;

    public ConvertQuoteToInvoiceCommandHandler(
        IQuoteToInvoiceConversionService conversionService,
        ICurrentUser currentUser)
    {
        _conversionService = conversionService;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(ConvertQuoteToInvoiceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId?.ToString() ?? "system";
        return await _conversionService.ConvertAsync(
            request.QuoteId,
            request.Options,
            userId,
            cancellationToken);
    }
}
