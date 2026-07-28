using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Invoices.Queries;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Reports;

/// <summary>
/// Imprime un état Studio enregistré. Réutilise le chemin d'exécution existant (le même que
/// <c>RunSavedReportQuery</c>) puis délègue le rendu au layout générique QuestPDF : aucune logique
/// de calcul dupliquée, donc l'état imprimé est exactement l'état affiché.
/// </summary>
public sealed record ExportStudioReportPdfQuery(Guid Id) : IRequest<Result<InvoicePdfResult>>;

public sealed class ExportStudioReportPdfQueryHandler
    : IRequestHandler<ExportStudioReportPdfQuery, Result<InvoicePdfResult>>
{
    private readonly ICustomReportRepository _reports;
    private readonly ICustomEntityRepository _entities;
    private readonly ICustomFieldRepository _fields;
    private readonly ICustomRecordRepository _records;
    private readonly IExistingDataSourceProvider _existing;
    private readonly ICompanyRepository _companies;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public ExportStudioReportPdfQueryHandler(
        ICustomReportRepository reports,
        ICustomEntityRepository entities,
        ICustomFieldRepository fields,
        ICustomRecordRepository records,
        IExistingDataSourceProvider existing,
        ICompanyRepository companies,
        IPdfService pdfService,
        IAuditService audit,
        ICurrentUser currentUser)
    {
        _reports = reports;
        _entities = entities;
        _fields = fields;
        _records = records;
        _existing = existing;
        _companies = companies;
        _pdfService = pdfService;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<InvoicePdfResult>> Handle(ExportStudioReportPdfQuery request, CancellationToken cancellationToken)
    {
        if (!StudioContext.TryGet(_currentUser, out var tenantId, out _, out var err))
            return Result.Failure<InvoicePdfResult>(err);

        var report = await _reports.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (report is null || !report.IsActive)
            return Result.Failure<InvoicePdfResult>(Error.NotFound("CustomReport", request.Id));

        var definition = ReportDefinitionJson.Parse(report.DefinitionJson);
        var runResult = await ReportExecutor.ExecuteAsync(tenantId, report.DataSourceKind, report.DataSourceRef,
            definition, _entities, _fields, _records, _existing, cancellationToken);
        if (runResult.IsFailure)
            return Result.Failure<InvoicePdfResult>(runResult.Error);

        var company = await _companies.GetDefaultAsync(cancellationToken);
        var sourceLabel = await ResolveSourceLabelAsync(tenantId, report.DataSourceKind, report.DataSourceRef, cancellationToken);

        var context = new StudioReportPdfContext(
            company?.Name ?? "Société",
            company?.Nif?.Value,
            report.DisplayName,
            sourceLabel,
            DescribeCriteria(definition, runResult.Value),
            runResult.Value);

        var pdfBytes = await _pdfService.GenerateStudioReportPdfAsync(context, cancellationToken);
        var fileName = $"Etat_{Sanitize(report.DisplayName)}_{DateTime.UtcNow:yyyy-MM-dd}.pdf";

        await _audit.LogAsync(AuditActions.Export.Pdf, "StudioReport", report.Id, cancellationToken: cancellationToken);

        return Result.Success(new InvoicePdfResult(pdfBytes, fileName, "application/pdf"));
    }

    private async Task<string?> ResolveSourceLabelAsync(
        Guid tenantId, CustomReportDataSourceKind kind, string dataSourceRef, CancellationToken ct)
    {
        if (kind == CustomReportDataSourceKind.ExistingSource)
            return ExistingDataSourceCatalog.Find(dataSourceRef)?.DisplayName ?? dataSourceRef;

        var entity = await _entities.GetByKeyAsync(tenantId, dataSourceRef, ct);
        return entity?.DisplayNamePlural ?? entity?.DisplayName ?? dataSourceRef;
    }

    /// <summary>Résume en français les critères de l'état pour l'en-tête imprimé (traçabilité).</summary>
    private static IReadOnlyList<string> DescribeCriteria(ReportDefinition definition, ReportResultDto result)
    {
        var labelByKey = result.Columns.ToDictionary(c => c.Key, c => c.Label, StringComparer.Ordinal);
        string Label(string key) => labelByKey.TryGetValue(key, out var l) ? l : key;

        var lines = new List<string>();

        if (definition.Filters.Count > 0)
        {
            var filters = definition.Filters.Select(f =>
                $"{Label(f.Field)} {OperatorLabel(f.Op)} {f.Value?.ToString() ?? string.Empty}"
                + (string.Equals(f.Op, "between", StringComparison.OrdinalIgnoreCase) && f.Value2 is not null
                    ? $" et {f.Value2}"
                    : string.Empty));
            lines.Add("Filtres : " + string.Join(" ; ", filters));
        }

        if (definition.Grouping.Count > 0)
            lines.Add("Regroupé par : " + string.Join(", ", definition.Grouping.Select(Label)));

        if (definition.Sort.Count > 0)
        {
            var sorts = definition.Sort.Select(s =>
                $"{Label(s.Field)} {(string.Equals(s.Dir, "desc", StringComparison.OrdinalIgnoreCase) ? "décroissant" : "croissant")}");
            lines.Add("Trié par : " + string.Join(", ", sorts));
        }

        return lines;
    }

    private static string OperatorLabel(string op) => op.ToLowerInvariant() switch
    {
        "eq" => "=",
        "neq" => "≠",
        "gt" => ">",
        "gte" => "≥",
        "lt" => "<",
        "lte" => "≤",
        "contains" => "contient",
        "in" => "parmi",
        "between" => "entre",
        _ => op
    };

    private static string Sanitize(string name)
    {
        var cleaned = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return cleaned.Trim('_') is { Length: > 0 } trimmed ? trimmed : "studio";
    }
}
