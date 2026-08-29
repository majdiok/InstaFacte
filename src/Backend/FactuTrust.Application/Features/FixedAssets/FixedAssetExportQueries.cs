using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.FixedAssets;

public sealed record ExportFixedAssetScheduleExcelQuery(Guid Id) : IRequest<Result<byte[]>>;

public sealed class ExportFixedAssetScheduleExcelQueryHandler : IRequestHandler<ExportFixedAssetScheduleExcelQuery, Result<byte[]>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IFixedAssetExportService _export;
    private readonly IFixedAssetSettingsRepository? _settings;

    public ExportFixedAssetScheduleExcelQueryHandler(
        IFixedAssetRepository assets,
        IFixedAssetExportService export,
        IFixedAssetSettingsRepository? settings = null)
    {
        _assets = assets;
        _export = export;
        _settings = settings;
    }

    public async Task<Result<byte[]>> Handle(ExportFixedAssetScheduleExcelQuery request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetByIdAsync(request.Id, includeSchedule: true, cancellationToken: cancellationToken);
        if (asset is null)
            return Result.Failure<byte[]>(Error.Validation("FixedAsset", "Immobilisation introuvable."));

        var settings = await FixedAssetFiscalYearSupport.GetSettingsAsync(_settings, cancellationToken);
        var dto = GenerateDepreciationScheduleCommandHandler.ToScheduleDto(asset);
        return Result.Success(_export.ExportScheduleToExcel(dto, settings.FiscalYearStartMonth, settings.FiscalYearLabelFormat));
    }
}

public sealed record ExportDepreciationReportExcelQuery(int FiscalYear) : IRequest<Result<byte[]>>;

public sealed class ExportDepreciationReportExcelQueryHandler : IRequestHandler<ExportDepreciationReportExcelQuery, Result<byte[]>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IFixedAssetExportService _export;
    private readonly IFixedAssetSettingsRepository? _settings;

    public ExportDepreciationReportExcelQueryHandler(
        IFixedAssetRepository assets,
        IFixedAssetExportService export,
        IFixedAssetSettingsRepository? settings = null)
    {
        _assets = assets;
        _export = export;
        _settings = settings;
    }

    public async Task<Result<byte[]>> Handle(ExportDepreciationReportExcelQuery request, CancellationToken cancellationToken)
    {
        // Frontière d'exercice configurée par tenant (P4) : l'éligibilité des biens et la sélection de
        // la ligne d'exercice utilisent le mois de début configuré. En exercice civil (mois 1), la
        // comparaison se réduit à InServiceDate.Year <= fiscalYear (parité avec le comportement antérieur).
        var settings = await FixedAssetFiscalYearSupport.GetSettingsAsync(_settings, cancellationToken);
        var (items, _) = await _assets.SearchAsync(
            1, 500, null, null, request.FiscalYear, null, settings.FiscalYearStartMonth, cancellationToken);
        var schedules = new List<FixedAssetScheduleDto>();
        foreach (var item in items)
        {
            var full = await _assets.GetByIdAsync(item.Id, includeSchedule: true, cancellationToken: cancellationToken);
            if (full is null)
                continue;
            var dto = GenerateDepreciationScheduleCommandHandler.ToScheduleDto(full);
            if (dto.Lines.Any(l => l.FiscalYear == request.FiscalYear))
                schedules.Add(dto);
        }

        return Result.Success(_export.ExportDepreciationReportToExcel(
            schedules, request.FiscalYear, settings.FiscalYearStartMonth, settings.FiscalYearLabelFormat));
    }
}

public sealed record ExportAmortizationReportExcelQuery(
    int? FiscalYear,
    AmortizationReportGroupingMode GroupingMode,
    FixedAssetStatus? Status,
    Guid? CategoryId,
    string? Search,
    string? CompanyName) : IRequest<Result<byte[]>>;

public sealed class ExportAmortizationReportExcelQueryHandler
    : IRequestHandler<ExportAmortizationReportExcelQuery, Result<byte[]>>
{
    private readonly IFixedAssetRepository _assets;
    private readonly IFixedAssetExportService _export;
    private readonly IFixedAssetSettingsRepository? _settings;

    public ExportAmortizationReportExcelQueryHandler(
        IFixedAssetRepository assets,
        IFixedAssetExportService export,
        IFixedAssetSettingsRepository? settings = null)
    {
        _assets = assets;
        _export = export;
        _settings = settings;
    }

    public async Task<Result<byte[]>> Handle(ExportAmortizationReportExcelQuery request, CancellationToken cancellationToken)
    {
        var fiscalYear = request.FiscalYear ?? DateTime.UtcNow.Year;
        var settings = await FixedAssetFiscalYearSupport.GetSettingsAsync(_settings, cancellationToken);
        var report = await _assets.GetAmortizationReportAsync(
            fiscalYear,
            request.GroupingMode,
            request.Status,
            request.CategoryId,
            request.Search,
            request.CompanyName ?? string.Empty,
            settings.FiscalYearStartMonth,
            cancellationToken);

        return Result.Success(_export.ExportAmortizationReportToExcel(report, settings.FiscalYearStartMonth, settings.FiscalYearLabelFormat));
    }
}
