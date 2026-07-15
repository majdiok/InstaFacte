using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Storefront.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Storefront;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Storefront.TenantAdmin.Commands;

/// <summary>
/// Command issued by an authenticated tenant admin to create a public storefront profile
/// for their tenant. The profile is created in <see cref="StorefrontStatus.Draft"/> state and
/// must subsequently be completed and submitted for review.
/// </summary>
/// <remarks>
/// Security guarantees:
/// <list type="bullet">
///   <item>Requires the <c>StorefrontTenantOwner</c> authorization policy (enforced at controller level).</item>
///   <item>Re-validates the current terms version against server-side <see cref="StorefrontOptions.CurrentTermsVersion"/>.</item>
///   <item>Consent is recorded synchronously with the profile in the Master DB.</item>
///   <item>Slug uniqueness is enforced atomically via the database unique index.</item>
/// </list>
/// </remarks>
public sealed record OptInStorefrontCommand(OptInStorefrontRequest Request) : IRequest<Result<StorefrontProfileDto>>;

public sealed class OptInStorefrontCommandValidator : AbstractValidator<OptInStorefrontCommand>
{
    public OptInStorefrontCommandValidator()
    {
        RuleFor(c => c.Request).NotNull();

        When(c => c.Request is not null, () =>
        {
            RuleFor(c => c.Request.Slug)
                .NotEmpty().WithMessage("Le slug public est obligatoire.")
                .MaximumLength(60).WithMessage("Le slug ne peut pas dépasser 60 caractères.");

            RuleFor(c => c.Request.DisplayName)
                .NotEmpty().WithMessage("Le nom public de la vitrine est obligatoire.")
                .MaximumLength(StorefrontProfile.DisplayNameMaxLength);

            RuleFor(c => c.Request.PublicContactEmail)
                .NotEmpty().WithMessage("Un email public de contact est obligatoire.")
                .EmailAddress().WithMessage("L'email public doit être valide.");

            RuleFor(c => c.Request.AcceptedTermsVersion)
                .NotEmpty().WithMessage("La version des CGU acceptée est obligatoire.")
                .MaximumLength(32);
        });
    }
}

public sealed class OptInStorefrontCommandHandler : IRequestHandler<OptInStorefrontCommand, Result<StorefrontProfileDto>>
{
    private readonly IStorefrontProfileRepository _profileRepository;
    private readonly IStorefrontSlugService _slugService;
    private readonly IIpAddressHasher _ipHasher;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly StorefrontOptions _options;

    public OptInStorefrontCommandHandler(
        IStorefrontProfileRepository profileRepository,
        IStorefrontSlugService slugService,
        IIpAddressHasher ipHasher,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IOptions<StorefrontOptions> options)
    {
        _profileRepository = profileRepository;
        _slugService = slugService;
        _ipHasher = ipHasher;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<Result<StorefrontProfileDto>> Handle(OptInStorefrontCommand request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<StorefrontProfileDto>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var tenantId = _tenantContext.TenantId ?? _currentUser.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<StorefrontProfileDto>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
            return Result.Failure<StorefrontProfileDto>(Error.Unauthorized("Utilisateur non authentifié."));

        var payload = request.Request;

        if (!string.Equals(payload.AcceptedTermsVersion?.Trim(), _options.CurrentTermsVersion, StringComparison.Ordinal))
            return Result.Failure<StorefrontProfileDto>(Error.Validation(
                "AcceptedTermsVersion",
                $"La version des CGU acceptée ne correspond pas à la version en vigueur ({_options.CurrentTermsVersion})."));

        var existing = await _profileRepository.GetByTenantIdAsync(tenantId.Value, cancellationToken);
        if (existing is not null)
            return Result.Failure<StorefrontProfileDto>(Error.Conflict("Une vitrine publique existe déjà pour cette société."));

        var publishedCount = await _profileRepository.CountPublishedAsync(cancellationToken);
        if (publishedCount >= _options.MaxPublishedStorefronts)
            return Result.Failure<StorefrontProfileDto>(Error.Validation(
                "Storefront",
                "La capacité maximale de vitrines publiées a été atteinte. Veuillez réessayer ultérieurement."));

        if (!_slugService.IsValid(payload.Slug, out var slugError))
            return Result.Failure<StorefrontProfileDto>(Error.Validation("Slug", slugError ?? "Slug invalide."));

        var uniqueSlug = await _slugService.GenerateUniqueAsync(payload.Slug, excludingStorefrontId: null, cancellationToken);

        var profileResult = StorefrontProfile.Create(
            tenantId: tenantId.Value,
            slug: uniqueSlug,
            displayName: payload.DisplayName,
            publicContactEmail: payload.PublicContactEmail,
            category: payload.Category,
            facadeTheme: payload.FacadeTheme,
            consentVersion: _options.CurrentTermsVersion,
            consentAcceptedByUserId: userId.Value);

        if (profileResult.IsFailure)
            return Result.Failure<StorefrontProfileDto>(profileResult.Error);

        var profile = profileResult.Value;

        var ipHash = _ipHasher.Hash(_currentUser.IpAddress);
        var userAgent = _currentUser.UserAgent;

        var consentResult = StorefrontPublishingConsent.Record(
            tenantId: tenantId.Value,
            storefrontProfileId: profile.Id,
            acceptedByUserId: userId.Value,
            termsVersion: _options.CurrentTermsVersion,
            ipAddressHash: ipHash,
            userAgent: userAgent);

        if (consentResult.IsFailure)
            return Result.Failure<StorefrontProfileDto>(consentResult.Error);

        await _profileRepository.AddOptInAsync(profile, consentResult.Value, cancellationToken);

        return Result.Success(StorefrontProfileMapper.ToDto(profile));
    }
}
