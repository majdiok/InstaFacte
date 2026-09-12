using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.CurrencyCatalog;

/// <summary>
/// Catalogue des devises et de leurs taux de change.
///
/// <para>
/// Toutes les commandes sont gardées par <see cref="AccountingSettings.MultiCurrencyEnabled"/> :
/// drapeau éteint, le module est en lecture seule et rien ne peut être créé ni modifié. La devise
/// de tenue reste par ailleurs intouchable — pas de taux, pas de désactivation, code figé.
/// </para>
/// </summary>
internal static class CurrencyMapping
{
    /// <summary>Nombre de taux attendus pour un exercice : aucun pour la devise de tenue, 1 si fixe, 12 si mensuelle.</summary>
    public static int ExpectedRateCount(Currency currency) => currency switch
    {
        { IsFunctional: true } => 0,
        { RatePeriodicity: ExchangeRatePeriodicity.Mensuelle } => 12,
        _ => 1
    };

    public static CurrencyDto ToDto(Currency currency, int configuredRateCount) => new()
    {
        Id = currency.Id,
        Code = currency.Code,
        Label = currency.Label,
        DecimalPlaces = currency.DecimalPlaces,
        RatePeriodicity = (int)currency.RatePeriodicity,
        IsActive = currency.IsActive,
        IsFunctional = currency.IsFunctional,
        ConfiguredRateCount = configuredRateCount,
        ExpectedRateCount = ExpectedRateCount(currency)
    };

    public static Result<ExchangeRatePeriodicity> ParsePeriodicity(int value) =>
        Enum.IsDefined(typeof(ExchangeRatePeriodicity), value)
            ? Result.Success((ExchangeRatePeriodicity)value)
            : Result.Failure<ExchangeRatePeriodicity>(Error.Validation("RatePeriodicity",
                "La périodicité des taux doit valoir 0 (fixe) ou 1 (mensuelle)."));
}

// ── Queries ────────────────────────────────────────────────────────────────

public sealed record GetCurrenciesQuery(int FiscalYear, bool IncludeInactive) : IRequest<Result<IReadOnlyList<CurrencyDto>>>;

public sealed class GetCurrenciesQueryHandler : IRequestHandler<GetCurrenciesQuery, Result<IReadOnlyList<CurrencyDto>>>
{
    private readonly ICurrencyRepository _currencies;

    public GetCurrenciesQueryHandler(ICurrencyRepository currencies) => _currencies = currencies;

    public async Task<Result<IReadOnlyList<CurrencyDto>>> Handle(GetCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var currencies = await _currencies.GetAllAsync(request.IncludeInactive, cancellationToken);
        var counts = await _currencies.CountRatesByCurrencyAsync(request.FiscalYear, cancellationToken);

        var dtos = currencies
            .Select(c => CurrencyMapping.ToDto(c, counts.TryGetValue(c.Id, out var n) ? n : 0))
            .ToList();

        return Result.Success<IReadOnlyList<CurrencyDto>>(dtos);
    }
}

public sealed record GetCurrencyDetailQuery(Guid Id, int FiscalYear) : IRequest<Result<CurrencyDetailDto>>;

public sealed class GetCurrencyDetailQueryHandler : IRequestHandler<GetCurrencyDetailQuery, Result<CurrencyDetailDto>>
{
    private readonly ICurrencyRepository _currencies;

    public GetCurrencyDetailQueryHandler(ICurrencyRepository currencies) => _currencies = currencies;

    public async Task<Result<CurrencyDetailDto>> Handle(GetCurrencyDetailQuery request, CancellationToken cancellationToken)
    {
        var currency = await _currencies.GetByIdAsync(request.Id, cancellationToken);
        if (currency is null)
            return Result.Failure<CurrencyDetailDto>(Error.NotFound("Currency", request.Id));

        var rates = await _currencies.GetRatesAsync(currency.Id, request.FiscalYear, cancellationToken);

        return Result.Success(new CurrencyDetailDto
        {
            Currency = CurrencyMapping.ToDto(currency, rates.Count),
            FiscalYear = request.FiscalYear,
            Rates = rates.Select(r => new CurrencyExchangeRateDto
            {
                Id = r.Id,
                FiscalYear = r.FiscalYear,
                Month = r.Month,
                Rate = r.Rate
            }).ToList()
        });
    }
}

/// <summary>
/// Taux applicable à une devise et une date. Le calcul est délégué au résolveur, seule autorité :
/// l'écran affiche ainsi le taux qui sera réellement appliqué, borne de surcharge comprise.
/// </summary>
public sealed record ResolveExchangeRateQuery(string CurrencyCode, DateTime EntryDate)
    : IRequest<Result<ResolvedExchangeRateDto>>;

public sealed class ResolveExchangeRateQueryHandler
    : IRequestHandler<ResolveExchangeRateQuery, Result<ResolvedExchangeRateDto>>
{
    private readonly IExchangeRateResolver _resolver;

    public ResolveExchangeRateQueryHandler(IExchangeRateResolver resolver) => _resolver = resolver;

    public async Task<Result<ResolvedExchangeRateDto>> Handle(ResolveExchangeRateQuery request, CancellationToken cancellationToken)
    {
        // requestedRate = null : on demande le taux de la table, jamais une surcharge.
        var resolved = await _resolver.ResolveAsync(request.CurrencyCode, request.EntryDate, null, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<ResolvedExchangeRateDto>(resolved.Error);

        var r = resolved.Value;
        return Result.Success(new ResolvedExchangeRateDto
        {
            CurrencyCode = r.CurrencyCode,
            Rate = r.Rate,
            ReferenceRate = r.ReferenceRate,
            IsOverridden = r.IsOverridden,
            IsFunctional = r.IsFunctional
        });
    }
}

// ── Commands ───────────────────────────────────────────────────────────────

public sealed record CreateCurrencyCommand(CreateCurrencyRequest Request) : IRequest<Result<Guid>>;

public sealed class CreateCurrencyCommandHandler : IRequestHandler<CreateCurrencyCommand, Result<Guid>>
{
    private readonly ICurrencyRepository _currencies;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public CreateCurrencyCommandHandler(ICurrencyRepository currencies, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _currencies = currencies;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<Guid>> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.MultiCurrencyEnabled)
            return Result.Failure<Guid>(Error.Validation("Currency", "La gestion multi-devises n'est pas activée."));

        var r = request.Request;

        var periodicity = CurrencyMapping.ParsePeriodicity(r.RatePeriodicity);
        if (periodicity.IsFailure)
            return Result.Failure<Guid>(periodicity.Error);

        var existing = await _currencies.GetByCodeAsync(r.Code ?? string.Empty, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(Error.Conflict($"La devise {existing.Code} existe déjà."));

        var create = Currency.Create(r.Code ?? string.Empty, r.Label, r.DecimalPlaces, periodicity.Value);
        if (create.IsFailure)
            return Result.Failure<Guid>(create.Error);

        var currency = create.Value;
        currency.SetAuditInfo(_currentUser.Email ?? "system", false);
        await _currencies.AddAsync(currency, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.CurrencyCreated, "Currency", currency.Id,
            newValues: new { currency.Code, currency.Label, currency.DecimalPlaces, currency.RatePeriodicity },
            cancellationToken: cancellationToken);

        return Result.Success(currency.Id);
    }
}

public sealed record UpdateCurrencyCommand(Guid Id, UpdateCurrencyRequest Request) : IRequest<Result>;

public sealed class UpdateCurrencyCommandHandler : IRequestHandler<UpdateCurrencyCommand, Result>
{
    private readonly ICurrencyRepository _currencies;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public UpdateCurrencyCommandHandler(ICurrencyRepository currencies, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _currencies = currencies;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(UpdateCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.MultiCurrencyEnabled)
            return Result.Failure(Error.Validation("Currency", "La gestion multi-devises n'est pas activée."));

        var currency = await _currencies.GetByIdAsync(request.Id, cancellationToken);
        if (currency is null)
            return Result.Failure(Error.NotFound("Currency", request.Id));

        var periodicity = CurrencyMapping.ParsePeriodicity(request.Request.RatePeriodicity);
        if (periodicity.IsFailure)
            return Result.Failure(periodicity.Error);

        var update = currency.Update(request.Request.Label, request.Request.DecimalPlaces, periodicity.Value);
        if (update.IsFailure)
            return update;

        currency.SetAuditInfo(_currentUser.Email ?? "system", isUpdate: true);
        await _currencies.UpdateAsync(currency, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.CurrencyUpdated, "Currency", currency.Id,
            newValues: new { currency.Code, currency.Label, currency.DecimalPlaces, currency.RatePeriodicity },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record ToggleCurrencyCommand(Guid Id) : IRequest<Result>;

public sealed class ToggleCurrencyCommandHandler : IRequestHandler<ToggleCurrencyCommand, Result>
{
    private readonly ICurrencyRepository _currencies;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingSettings _settings;

    public ToggleCurrencyCommandHandler(ICurrencyRepository currencies, IAuditService auditService, ICurrentUser currentUser, IOptions<AccountingSettings> settings)
    {
        _currencies = currencies;
        _auditService = auditService;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(ToggleCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.MultiCurrencyEnabled)
            return Result.Failure(Error.Validation("Currency", "La gestion multi-devises n'est pas activée."));

        var currency = await _currencies.GetByIdAsync(request.Id, cancellationToken);
        if (currency is null)
            return Result.Failure(Error.NotFound("Currency", request.Id));

        if (currency.IsActive)
        {
            var deactivate = currency.Deactivate();
            if (deactivate.IsFailure)
                return deactivate;
        }
        else
        {
            currency.Activate();
        }

        currency.SetAuditInfo(_currentUser.Email ?? "system", isUpdate: true);
        await _currencies.UpdateAsync(currency, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.CurrencyToggled, "Currency", currency.Id,
            newValues: new { currency.Code, currency.IsActive }, cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record SaveCurrencyRatesCommand(Guid CurrencyId, SaveCurrencyRatesRequest Request) : IRequest<Result>;

public sealed class SaveCurrencyRatesCommandHandler : IRequestHandler<SaveCurrencyRatesCommand, Result>
{
    private readonly ICurrencyRepository _currencies;
    private readonly IAuditService _auditService;
    private readonly AccountingSettings _settings;

    public SaveCurrencyRatesCommandHandler(ICurrencyRepository currencies, IAuditService auditService, IOptions<AccountingSettings> settings)
    {
        _currencies = currencies;
        _auditService = auditService;
        _settings = settings.Value;
    }

    public async Task<Result> Handle(SaveCurrencyRatesCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.MultiCurrencyEnabled)
            return Result.Failure(Error.Validation("Currency", "La gestion multi-devises n'est pas activée."));

        var currency = await _currencies.GetByIdAsync(request.CurrencyId, cancellationToken);
        if (currency is null)
            return Result.Failure(Error.NotFound("Currency", request.CurrencyId));

        if (currency.IsFunctional)
            return Result.Failure(Error.Validation("Currency",
                "La devise de tenue des comptes n'a pas de taux de change."));

        var fiscalYear = request.Request.FiscalYear;
        var monthly = currency.RatePeriodicity == ExchangeRatePeriodicity.Mensuelle;

        var rates = new List<CurrencyExchangeRate>();
        var seen = new HashSet<int?>();

        foreach (var entry in request.Request.Rates)
        {
            // Une devise mensuelle n'accepte que des mois ; une devise fixe n'accepte que le taux
            // annuel. Sans ce contrôle, un changement de périodicité laisserait des lignes
            // orphelines qu'aucun écran ne saurait plus afficher ni corriger.
            if (monthly && entry.Month is null)
                return Result.Failure(Error.Validation("Month",
                    $"La devise {currency.Code} est configurée en taux mensuels : chaque taux doit porter un mois."));

            if (!monthly && entry.Month is not null)
                return Result.Failure(Error.Validation("Month",
                    $"La devise {currency.Code} est configurée en taux fixe : un seul taux, sans mois."));

            if (!seen.Add(entry.Month))
                return Result.Failure(Error.Validation("Month",
                    "Deux taux ne peuvent pas viser la même période."));

            // Taux vide ou nul : la période reste non configurée, la ligne n'est pas créée.
            if (entry.Rate is null or <= 0)
                continue;

            var create = CurrencyExchangeRate.Create(currency.Id, fiscalYear, entry.Month, entry.Rate.Value);
            if (create.IsFailure)
                return Result.Failure(create.Error);

            rates.Add(create.Value);
        }

        await _currencies.ReplaceRatesAsync(currency.Id, fiscalYear, rates, cancellationToken);

        await _auditService.LogAsync(AuditActions.Accounting.CurrencyRatesSaved, "Currency", currency.Id,
            newValues: new { currency.Code, FiscalYear = fiscalYear, RateCount = rates.Count },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
