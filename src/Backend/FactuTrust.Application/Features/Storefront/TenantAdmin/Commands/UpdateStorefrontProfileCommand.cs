using System.Text.RegularExpressions;
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
/// Updates the tenant's storefront profile (branding + public contacts). Does not change the
/// workflow status. A profile in <see cref="StorefrontStatus.Suspended"/> state cannot be updated
/// (the domain enforces this); callers must contact platform support.
/// </summary>
public sealed record UpdateStorefrontProfileCommand(UpdateStorefrontProfileRequest Request)
    : IRequest<Result<StorefrontProfileDto>>;

public sealed partial class UpdateStorefrontProfileCommandValidator : AbstractValidator<UpdateStorefrontProfileCommand>
{
    private const string HexColorPattern = "^#[0-9A-Fa-f]{6}$";

    [GeneratedRegex(HexColorPattern, RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();

    public UpdateStorefrontProfileCommandValidator()
    {
        RuleFor(c => c.Request).NotNull();

        When(c => c.Request is not null, () =>
        {
            RuleFor(c => c.Request.DisplayName)
                .NotEmpty().MaximumLength(StorefrontProfile.DisplayNameMaxLength);

            RuleFor(c => c.Request.Tagline)
                .MaximumLength(StorefrontProfile.TaglineMaxLength);

            RuleFor(c => c.Request.DescriptionMarkdown)
                .MaximumLength(StorefrontProfile.DescriptionMaxLength);

            RuleFor(c => c.Request.BrandPrimaryColorHex)
                .NotEmpty()
                .Must(v => !string.IsNullOrWhiteSpace(v) && HexColorRegex().IsMatch(v))
                .WithMessage("La couleur primaire doit être au format hexadécimal #RRGGBB.");

            RuleFor(c => c.Request.BrandSecondaryColorHex)
                .NotEmpty()
                .Must(v => !string.IsNullOrWhiteSpace(v) && HexColorRegex().IsMatch(v))
                .WithMessage("La couleur secondaire doit être au format hexadécimal #RRGGBB.");

            RuleFor(c => c.Request.PublicContactEmail)
                .NotEmpty().EmailAddress();

            RuleFor(c => c.Request.PublicContactPhone)
                .MaximumLength(32);

            RuleFor(c => c.Request.PublicContactWhatsApp)
                .MaximumLength(32);

            RuleFor(c => c.Request.PublicLogoUrl)
                .MaximumLength(500);

            RuleFor(c => c.Request.PublicCoverImageUrl)
                .MaximumLength(500);
        });
    }
}

public sealed class UpdateStorefrontProfileCommandHandler
    : IRequestHandler<UpdateStorefrontProfileCommand, Result<StorefrontProfileDto>>
{
    private readonly IStorefrontProfileRepository _profileRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly StorefrontOptions _options;

    public UpdateStorefrontProfileCommandHandler(
        IStorefrontProfileRepository profileRepository,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IOptions<StorefrontOptions> options)
    {
        _profileRepository = profileRepository;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<Result<StorefrontProfileDto>> Handle(
        UpdateStorefrontProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Result.Failure<StorefrontProfileDto>(Error.Forbidden("La fonctionnalité Rue virtuelle est désactivée."));

        var tenantId = _tenantContext.TenantId ?? _currentUser.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<StorefrontProfileDto>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var profile = await _profileRepository.GetByTenantIdAsync(tenantId.Value, cancellationToken);
        if (profile is null)
            return Result.Failure<StorefrontProfileDto>(
                Error.NotFound(nameof(StorefrontProfile), tenantId.Value));

        var payload = request.Request;
        var updateResult = profile.UpdateProfile(
            displayName: payload.DisplayName,
            tagline: payload.Tagline,
            descriptionMarkdown: payload.DescriptionMarkdown,
            brandPrimaryColorHex: payload.BrandPrimaryColorHex,
            brandSecondaryColorHex: payload.BrandSecondaryColorHex,
            category: payload.Category,
            facadeTheme: payload.FacadeTheme,
            publicContactEmail: payload.PublicContactEmail,
            publicContactPhone: payload.PublicContactPhone,
            publicContactWhatsApp: payload.PublicContactWhatsApp,
            publicLogoUrl: payload.PublicLogoUrl,
            publicCoverImageUrl: payload.PublicCoverImageUrl,
            orderSubmissionEnabled: payload.OrderSubmissionEnabled);

        if (updateResult.IsFailure)
            return Result.Failure<StorefrontProfileDto>(updateResult.Error);

        await _profileRepository.UpdateAsync(profile, cancellationToken);

        return Result.Success(StorefrontProfileMapper.ToDto(profile));
    }
}
