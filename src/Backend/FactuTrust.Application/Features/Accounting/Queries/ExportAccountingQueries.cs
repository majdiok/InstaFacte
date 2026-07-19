using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

// Le suffixe « Csv » des records est conservé pour compatibilité (référencés par le contrôleur) ;
// le paramètre Format (défaut Csv) aiguille désormais vers CSV / Excel / PDF sans régression.

public sealed record ExportJournalCsvQuery(string? JournalCode, DateTime From, DateTime To, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportJournalCsvQueryHandler
    : IRequestHandler<ExportJournalCsvQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportJournalCsvQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportJournalCsvQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetJournalEntriesAsync(request.JournalCode, request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportJournalToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateJournalPdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    string.IsNullOrWhiteSpace(request.JournalCode) ? "Journal général" : $"Journal {request.JournalCode}",
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportJournalToCsv(result.Value)
        };
    }
}

public sealed record ExportLedgerCsvQuery(string AccountNumber, DateTime From, DateTime To, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportLedgerCsvQueryHandler
    : IRequestHandler<ExportLedgerCsvQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportLedgerCsvQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportLedgerCsvQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetLedgerAsync(request.AccountNumber, request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportLedgerToExcel(result.Value, request.AccountNumber),
            AccountingExportFormat.Pdf => await _pdf.GenerateLedgerPdfAsync(
                request.AccountNumber,
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    $"Grand livre — compte {request.AccountNumber}",
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportLedgerToCsv(result.Value)
        };
    }
}

public sealed record ExportBalanceCsvQuery(DateTime From, DateTime To, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportBalanceCsvQueryHandler
    : IRequestHandler<ExportBalanceCsvQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportBalanceCsvQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportBalanceCsvQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetBalanceAsync(request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportBalanceToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateBalancePdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    "Balance générale",
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportBalanceToCsv(result.Value)
        };
    }
}
