using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Fiscal;

// ── Export de la détermination du résultat fiscal ────────────────────────────────────────

public sealed record ExportFiscalResultQuery(int FiscalYear, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportFiscalResultQueryHandler : IRequestHandler<ExportFiscalResultQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportFiscalResultQueryHandler(IMediator mediator, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _mediator = mediator;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportFiscalResultQuery request, CancellationToken cancellationToken)
    {
        var r = await _mediator.Send(new GetFiscalResultDeclarationQuery(request.FiscalYear), cancellationToken);
        if (r.IsFailure)
            return Result.Failure<byte[]>(r.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportFiscalResultToExcel(r.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateFiscalResultPdfAsync(
                r.Value,
                await BuildHeaderAsync("Détermination du résultat fiscal", request.FiscalYear, cancellationToken),
                cancellationToken),
            _ => _export.ExportFiscalResultToCsv(r.Value)
        };
    }

    private async Task<AccountingReportHeader> BuildHeaderAsync(string title, int fiscalYear, CancellationToken ct)
    {
        var company = await _companies.GetDefaultAsync(ct);
        return new AccountingReportHeader(company?.Name ?? "Société", company?.VatCode, title, $"Exercice {fiscalYear}");
    }
}

// ── Export de la liasse consolidée ───────────────────────────────────────────────────────

public sealed record ExportConsolidatedLiasseQuery(int FiscalYear, AccountingExportFormat Format = AccountingExportFormat.Pdf)
    : IRequest<Result<byte[]>>;

public sealed class ExportConsolidatedLiasseQueryHandler : IRequestHandler<ExportConsolidatedLiasseQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportConsolidatedLiasseQueryHandler(IMediator mediator, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _mediator = mediator;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportConsolidatedLiasseQuery request, CancellationToken cancellationToken)
    {
        var r = await _mediator.Send(new GetConsolidatedLiasseQuery(request.FiscalYear), cancellationToken);
        if (r.IsFailure)
            return Result.Failure<byte[]>(r.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportConsolidatedLiasseToExcel(r.Value),
            AccountingExportFormat.Csv => _export.ExportFiscalResultToCsv(r.Value.FiscalResult),
            _ => await _pdf.GenerateConsolidatedLiassePdfAsync(
                r.Value,
                await BuildHeaderAsync("Liasse fiscale", request.FiscalYear, r.Value.CompanyName, cancellationToken),
                cancellationToken)
        };
    }

    private async Task<AccountingReportHeader> BuildHeaderAsync(string title, int fiscalYear, string fallbackName, CancellationToken ct)
    {
        var company = await _companies.GetDefaultAsync(ct);
        return new AccountingReportHeader(company?.Name ?? fallbackName, company?.VatCode, title, $"Exercice {fiscalYear}");
    }
}

// ── Export du livre d'inventaire ─────────────────────────────────────────────────────────

public sealed record ExportInventoryBookQuery(int FiscalYear, AccountingExportFormat Format = AccountingExportFormat.Pdf)
    : IRequest<Result<byte[]>>;

public sealed class ExportInventoryBookQueryHandler : IRequestHandler<ExportInventoryBookQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportInventoryBookQueryHandler(IMediator mediator, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _mediator = mediator;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportInventoryBookQuery request, CancellationToken cancellationToken)
    {
        var r = await _mediator.Send(new GetInventoryBookQuery(request.FiscalYear), cancellationToken);
        if (r.IsFailure)
            return Result.Failure<byte[]>(r.Error);

        var company = await _companies.GetDefaultAsync(cancellationToken);
        var header = new AccountingReportHeader(
            company?.Name ?? r.Value.CompanyName, company?.VatCode,
            "Livre d'inventaire", $"Exercice {request.FiscalYear}");

        return request.Format switch
        {
            // Excel/CSV : repli sur la balance de clôture (le PDF reste l'édition légale de référence).
            AccountingExportFormat.Excel => _export.ExportBalanceToExcel(r.Value.ClosingBalance),
            AccountingExportFormat.Csv => _export.ExportBalanceToCsv(r.Value.ClosingBalance),
            _ => await _pdf.GenerateInventoryBookPdfAsync(r.Value, header, cancellationToken)
        };
    }
}
