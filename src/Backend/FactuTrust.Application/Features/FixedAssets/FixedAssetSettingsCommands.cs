using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.FixedAssets;

// ---------------------------------------------------------------
// Lecture / écriture des paramètres Immobilisations du dossier
// (plan « Exercices décalés », P1) — permission Accounting.
// ---------------------------------------------------------------

public sealed record GetFixedAssetSettingsQuery : IRequest<Result<FixedAssetSettingsDto>>;

public sealed class GetFixedAssetSettingsQueryHandler
    : IRequestHandler<GetFixedAssetSettingsQuery, Result<FixedAssetSettingsDto>>
{
    private readonly IFixedAssetSettingsRepository _settings;
    private readonly IFiscalYearResolver _resolver;

    public GetFixedAssetSettingsQueryHandler(
        IFixedAssetSettingsRepository settings,
        IFiscalYearResolver resolver)
    {
        _settings = settings;
        _resolver = resolver;
    }

    public async Task<Result<FixedAssetSettingsDto>> Handle(
        GetFixedAssetSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await _settings.GetForTenantAsync(cancellationToken);
        return Result.Success(ToDto(settings, _resolver));
    }

    internal static FixedAssetSettingsDto ToDto(FixedAssetSettings settings, IFiscalYearResolver resolver)
    {
        // Libellé d'exemple pour l'année courante (aide l'UI à prévisualiser le format).
        var currentKey = resolver.FiscalYearKey(DateTime.UtcNow, settings.FiscalYearStartMonth);
        var sample = resolver.FiscalYearLabel(currentKey, settings.FiscalYearStartMonth, settings.FiscalYearLabelFormat);
        return new FixedAssetSettingsDto(
            settings.FiscalYearStartMonth,
            settings.FiscalYearLabelFormat,
            sample);
    }
}

public sealed record UpdateFixedAssetSettingsCommand(UpdateFixedAssetSettingsRequest Request)
    : IRequest<Result<FixedAssetSettingsDto>>;

public sealed class UpdateFixedAssetSettingsCommandHandler
    : IRequestHandler<UpdateFixedAssetSettingsCommand, Result<FixedAssetSettingsDto>>
{
    private readonly IFixedAssetSettingsRepository _settings;
    private readonly IFiscalYearResolver _resolver;
    private readonly ICurrentUser _currentUser;

    public UpdateFixedAssetSettingsCommandHandler(
        IFixedAssetSettingsRepository settings,
        IFiscalYearResolver resolver,
        ICurrentUser currentUser)
    {
        _settings = settings;
        _resolver = resolver;
        _currentUser = currentUser;
    }

    public async Task<Result<FixedAssetSettingsDto>> Handle(
        UpdateFixedAssetSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var r = request.Request;

        // Validation stable au niveau domaine : mois ∈ [1,12], format ∈ {"N/N+1","N"}.
        var probe = FixedAssetSettings.Create(r.FiscalYearStartMonth, r.FiscalYearLabelFormat);
        if (probe.IsFailure)
            return Result.Failure<FixedAssetSettingsDto>(probe.Error);

        var persisted = await _settings.UpsertAsync(
            r.FiscalYearStartMonth,
            r.FiscalYearLabelFormat,
            _currentUser.Email ?? "system",
            cancellationToken);

        return Result.Success(GetFixedAssetSettingsQueryHandler.ToDto(persisted, _resolver));
    }
}
