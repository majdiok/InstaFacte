using System.Globalization;
using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

// ── Balance âgée (clients / fournisseurs) ──────────────────────────────────────────────

public sealed record ExportAgingQuery(ThirdPartyKind Kind, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportAgingQueryHandler : IRequestHandler<ExportAgingQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportAgingQueryHandler(IMediator mediator, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _mediator = mediator;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportAgingQuery request, CancellationToken cancellationToken)
    {
        var data = request.Kind == ThirdPartyKind.Supplier
            ? await _mediator.Send(new GetSupplierAgingReportQuery(), cancellationToken)
            : await _mediator.Send(new GetClientAgingReportQuery(), cancellationToken);
        if (data.IsFailure)
            return Result.Failure<byte[]>(data.Error);

        var kindLabel = AccountingExportHelpers.KindLabel(request.Kind);
        var period = $"Au {DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportAgingToExcel(data.Value, kindLabel),
            AccountingExportFormat.Pdf => await _pdf.GenerateAgingPdfAsync(
                data.Value,
                AccountingExportHelpers.Header(await _companies.GetDefaultAsync(cancellationToken), $"Balance âgée — {kindLabel}", period),
                cancellationToken),
            _ => _export.ExportAgingToCsv(data.Value, kindLabel)
        };
    }
}

// ── Bilan ──────────────────────────────────────────────────────────────────────────────

public sealed record ExportBalanceSheetQuery(int FiscalYear, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportBalanceSheetQueryHandler : IRequestHandler<ExportBalanceSheetQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportBalanceSheetQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportBalanceSheetQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetBalanceSheetAsync(request.FiscalYear, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportBalanceSheetToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateBalanceSheetPdfAsync(
                result.Value,
                AccountingExportHelpers.Header(await _companies.GetDefaultAsync(cancellationToken), "Bilan", $"Exercice {request.FiscalYear}"),
                cancellationToken),
            _ => _export.ExportBalanceSheetToCsv(result.Value)
        };
    }
}

// ── Compte de résultat ─────────────────────────────────────────────────────────────────

public sealed record ExportIncomeStatementQuery(int FiscalYear, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportIncomeStatementQueryHandler : IRequestHandler<ExportIncomeStatementQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportIncomeStatementQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportIncomeStatementQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetIncomeStatementAsync(request.FiscalYear, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportIncomeStatementToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateIncomeStatementPdfAsync(
                result.Value,
                AccountingExportHelpers.Header(await _companies.GetDefaultAsync(cancellationToken), "Compte de résultat", $"Exercice {request.FiscalYear}"),
                cancellationToken),
            _ => _export.ExportIncomeStatementToCsv(result.Value)
        };
    }
}
