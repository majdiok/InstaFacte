using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.FixedAssets;

public sealed record GetDepreciationRateCategoriesQuery : IRequest<Result<IReadOnlyList<DepreciationRateCategoryDto>>>;

public sealed class GetDepreciationRateCategoriesQueryHandler
    : IRequestHandler<GetDepreciationRateCategoriesQuery, Result<IReadOnlyList<DepreciationRateCategoryDto>>>
{
    private readonly IDepreciationRateCategoryRepository _categories;

    public GetDepreciationRateCategoriesQueryHandler(IDepreciationRateCategoryRepository categories)
    {
        _categories = categories;
    }

    public async Task<Result<IReadOnlyList<DepreciationRateCategoryDto>>> Handle(
        GetDepreciationRateCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _categories.GetAllActiveAsync(cancellationToken);
        return Result.Success<IReadOnlyList<DepreciationRateCategoryDto>>(
            items.Select(FixedAssetMappings.ToDto).ToList());
    }
}

public sealed record GetFixedAssetsQuery(
    int Page,
    int PageSize,
    FixedAssetStatus? Status,
    Guid? CategoryId,
    int? FiscalYear,
    string? Search) : IRequest<Result<FixedAssetListResponse>>;

public sealed class GetFixedAssetsQueryHandler : IRequestHandler<GetFixedAssetsQuery, Result<FixedAssetListResponse>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IFixedAssetSettingsRepository? _settings;

    public GetFixedAssetsQueryHandler(IFixedAssetRepository assets, IFixedAssetSettingsRepository? settings = null)
    {
        _assets = assets;
        _settings = settings;
    }

    public async Task<Result<FixedAssetListResponse>> Handle(GetFixedAssetsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;
        // Filtre d'exercice par frontière décalée (P3) : le filtre fiscalYear optionnel de l'UI est
        // une clé d'exercice ; la comparaison d'éligibilité utilise le mois de début configuré.
        var startMonth = await FixedAssetFiscalYearSupport.GetStartMonthAsync(_settings, cancellationToken);
        var (items, total) = await _assets.SearchAsync(
            page, pageSize, request.Status, request.CategoryId, request.FiscalYear, request.Search, startMonth, cancellationToken);

        return Result.Success(new FixedAssetListResponse(
            items.Select(FixedAssetMappings.ToDto).ToList(),
            total,
            page,
            pageSize));
    }
}

public sealed record GetCurrentYearAmortizationTableQuery(
    int Page,
    int PageSize,
    int? FiscalYear,
    FixedAssetStatus? Status,
    Guid? CategoryId,
    string? Search) : IRequest<Result<FixedAssetAmortizationTableResponse>>;

public sealed class GetCurrentYearAmortizationTableQueryHandler
    : IRequestHandler<GetCurrentYearAmortizationTableQuery, Result<FixedAssetAmortizationTableResponse>>
{
    private readonly IFixedAssetRepository _assets;

    public GetCurrentYearAmortizationTableQueryHandler(IFixedAssetRepository assets)
    {
        _assets = assets;
    }

    public async Task<Result<FixedAssetAmortizationTableResponse>> Handle(
        GetCurrentYearAmortizationTableQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;
        var fiscalYear = request.FiscalYear ?? DateTime.UtcNow.Year;

        var (items, total) = await _assets.SearchCurrentYearAmortizationTableAsync(
            page,
            pageSize,
            fiscalYear,
            request.Status,
            request.CategoryId,
            request.Search,
            cancellationToken);

        return Result.Success(new FixedAssetAmortizationTableResponse(
            items,
            total,
            page,
            pageSize,
            fiscalYear));
    }
}

public sealed record GetFixedAssetByIdQuery(Guid Id) : IRequest<Result<FixedAssetDto>>;

public sealed class GetFixedAssetByIdQueryHandler : IRequestHandler<GetFixedAssetByIdQuery, Result<FixedAssetDto>>
{
    private readonly IFixedAssetRepository _assets;

    public GetFixedAssetByIdQueryHandler(IFixedAssetRepository assets)
    {
        _assets = assets;
    }

    public async Task<Result<FixedAssetDto>> Handle(GetFixedAssetByIdQuery request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetByIdAsync(request.Id, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<FixedAssetDto>(Error.Validation("FixedAsset", "Immobilisation introuvable."));
        return Result.Success(FixedAssetMappings.ToDto(asset));
    }
}

public sealed record GetFixedAssetScheduleQuery(Guid Id) : IRequest<Result<FixedAssetScheduleDto>>;

public sealed class GetFixedAssetScheduleQueryHandler : IRequestHandler<GetFixedAssetScheduleQuery, Result<FixedAssetScheduleDto>>
{
    private readonly IFixedAssetRepository _assets;

    public GetFixedAssetScheduleQueryHandler(IFixedAssetRepository assets)
    {
        _assets = assets;
    }

    public async Task<Result<FixedAssetScheduleDto>> Handle(GetFixedAssetScheduleQuery request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<FixedAssetScheduleDto>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        // État d'extourne des lignes (T13, C6) : alimente IsReversed du DTO, pour que le frontend
        // ne bloque la régénération que sur les dotations nettes (IsPosted && !IsReversed).
        var reversalState = await _assets.GetScheduleLinesWithReversalStateAsync(request.Id, cancellationToken);

        return Result.Success(GenerateDepreciationScheduleCommandHandler.ToScheduleDto(asset, reversalState));
    }
}

public sealed record GetAmortizationReportQuery(
    int? FiscalYear,
    AmortizationReportGroupingMode GroupingMode,
    FixedAssetStatus? Status,
    Guid? CategoryId,
    string? Search,
    string? CompanyName) : IRequest<Result<AmortizationReportResponse>>;

public sealed class GetAmortizationReportQueryHandler
    : IRequestHandler<GetAmortizationReportQuery, Result<AmortizationReportResponse>>
{
    private readonly IFixedAssetRepository _assets;

    public GetAmortizationReportQueryHandler(IFixedAssetRepository assets)
    {
        _assets = assets;
    }

    public async Task<Result<AmortizationReportResponse>> Handle(
        GetAmortizationReportQuery request,
        CancellationToken cancellationToken)
    {
        var fiscalYear = request.FiscalYear ?? DateTime.UtcNow.Year;
        var report = await _assets.GetAmortizationReportAsync(
            fiscalYear,
            request.GroupingMode,
            request.Status,
            request.CategoryId,
            request.Search,
            request.CompanyName ?? string.Empty,
            cancellationToken);

        return Result.Success(report);
    }
}