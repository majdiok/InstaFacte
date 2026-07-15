using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.Public;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.Public.Commands;

public sealed record SubmitPublicStorefrontOrderCommand(
    SubmitPublicStorefrontOrderRequest Request,
    string? TurnstileToken,
    string? RemoteIp,
    string? UserAgent) : IRequest<Result<SubmitPublicStorefrontOrderResponse>>;

public sealed class SubmitPublicStorefrontOrderCommandValidator : AbstractValidator<SubmitPublicStorefrontOrderCommand>
{
    public SubmitPublicStorefrontOrderCommandValidator()
    {
        RuleFor(c => c.Request).NotNull();
        When(c => c.Request is not null, () =>
        {
            RuleFor(c => c.Request.GuestFullName).NotEmpty().MaximumLength(StorefrontOrder.GuestNameMaxLength);
            RuleFor(c => c.Request.GuestEmail).NotEmpty().EmailAddress();
            RuleFor(c => c.Request.GuestPhone).NotEmpty().MaximumLength(30);
            RuleFor(c => c.Request.DeliveryStreet).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Request.DeliveryCity).NotEmpty().MaximumLength(100);
            RuleFor(c => c.Request.DeliveryGovernorate).NotEmpty().MaximumLength(100);
            RuleFor(c => c.Request.DeliveryCountry).NotEmpty().MaximumLength(100);
            RuleFor(c => c.Request.Lines).NotEmpty();
            RuleForEach(c => c.Request.Lines).ChildRules(line =>
            {
                line.RuleFor(l => l.StorefrontProductId).NotEmpty();
                line.RuleFor(l => l.Quantity).GreaterThan(0);
            });
        });
    }
}

public sealed class SubmitPublicStorefrontOrderCommandHandler
    : IRequestHandler<SubmitPublicStorefrontOrderCommand, Result<SubmitPublicStorefrontOrderResponse>>
{
    private readonly IPublicStorefrontReadRepository _read;
    private readonly IStorefrontOrderRepository _orders;
    private readonly IStorefrontProfileRepository _profiles;
    private readonly IIpAddressHasher _ipHasher;
    private readonly IStorefrontCaptchaValidator _captcha;
    private readonly StorefrontOptions _options;

    public SubmitPublicStorefrontOrderCommandHandler(
        IPublicStorefrontReadRepository read,
        IStorefrontOrderRepository orders,
        IStorefrontProfileRepository profiles,
        IIpAddressHasher ipHasher,
        IStorefrontCaptchaValidator captcha,
        IOptions<StorefrontOptions> options)
    {
        _read = read;
        _orders = orders;
        _profiles = profiles;
        _ipHasher = ipHasher;
        _captcha = captcha;
        _options = options.Value;
    }

    public async Task<Result<SubmitPublicStorefrontOrderResponse>> Handle(
        SubmitPublicStorefrontOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<SubmitPublicStorefrontOrderResponse>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        if (!await _captcha.IsValidAsync(request.TurnstileToken, cancellationToken))
            return Result.Failure<SubmitPublicStorefrontOrderResponse>(Error.Validation("Captcha", "Vérification anti-bot invalide."));

        var payload = request.Request;
        var addressResult = Address.Create(
            payload.DeliveryStreet,
            payload.DeliveryCity,
            payload.DeliveryGovernorate,
            payload.DeliveryStreetLine2,
            payload.DeliveryPostalCode,
            payload.DeliveryCountry);

        if (addressResult.IsFailure)
            return Result.Failure<SubmitPublicStorefrontOrderResponse>(addressResult.Error);

        var ipHash = _ipHasher.Hash(request.RemoteIp);
        var uaHash = _ipHasher.Hash(request.UserAgent);

        var orderResult = StorefrontOrder.Create(
            payload.GuestFullName,
            payload.GuestEmail,
            payload.GuestPhone,
            addressResult.Value,
            payload.GuestNotes,
            ipHash,
            uaHash);

        if (orderResult.IsFailure)
            return Result.Failure<SubmitPublicStorefrontOrderResponse>(orderResult.Error);

        var order = orderResult.Value;

        var productIds = payload.Lines.Select(l => l.StorefrontProductId).Distinct().ToList();
        var snapshots = await _read.GetProductSnapshotsAsync(productIds, cancellationToken);
        if (snapshots.Count != productIds.Count)
            return Result.Failure<SubmitPublicStorefrontOrderResponse>(Error.Validation("Lines", "Un ou plusieurs articles ne sont pas disponibles."));

        foreach (var snap in snapshots)
        {
            if (!snap.IsVisible)
                return Result.Failure<SubmitPublicStorefrontOrderResponse>(Error.Validation("Lines", "Un article du panier n'est plus publié."));
        }

        var profileIds = snapshots.Select(s => s.StorefrontProfileId).Distinct().ToList();
        foreach (var profileId in profileIds)
        {
            var profile = await _profiles.GetByIdAsync(profileId, cancellationToken);
            if (profile is null || profile.Status != StorefrontStatus.Published || !profile.OrderSubmissionEnabled)
                return Result.Failure<SubmitPublicStorefrontOrderResponse>(Error.Validation("Order", "La commande en ligne n'est pas autorisée pour cette vitrine."));
        }

        foreach (var line in payload.Lines)
        {
            var snap = snapshots.First(s => s.StorefrontProductId == line.StorefrontProductId);
            var add = order.AddItem(
                snap.StorefrontProductId,
                snap.TenantId,
                snap.SourceProductId,
                snap.Name,
                line.Quantity,
                snap.UnitPriceAmount,
                snap.Currency);

            if (add.IsFailure)
                return Result.Failure<SubmitPublicStorefrontOrderResponse>(add.Error);
        }

        order.RaiseSubmittedEvent();
        order.MarkDispatched("{}");

        await _orders.AddAsync(order, cancellationToken);

        return Result.Success(new SubmitPublicStorefrontOrderResponse { OrderId = order.Id });
    }
}
