using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
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

public sealed record ExportJournalSummaryQuery(
    DateTime From,
    DateTime To,
    JournalSummaryGrouping Grouping,
    string? JournalCode = null,
    AccountingExportFormat Format = AccountingExportFormat.Csv) : IRequest<Result<byte[]>>;

public sealed class ExportJournalSummaryQueryHandler
    : IRequestHandler<ExportJournalSummaryQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportJournalSummaryQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    /// <summary>Intitulé de l'état, aligné sur les entrées du menu comptable.</summary>
    private static string TitleOf(JournalSummaryGrouping grouping) => grouping switch
    {
        JournalSummaryGrouping.Month => "Journal centralisateur",
        JournalSummaryGrouping.Account => "Récapitulation des journaux",
        _ => "Totaux des journaux"
    };

    public async Task<Result<byte[]>> Handle(ExportJournalSummaryQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetJournalSummaryAsync(
            request.From, request.To, request.Grouping, request.JournalCode, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportJournalSummaryToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateJournalSummaryPdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    TitleOf(request.Grouping),
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportJournalSummaryToCsv(result.Value)
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

public sealed record ExportDetailedBalanceQuery(
    string? AccountFrom,
    string? AccountTo,
    DateTime From,
    DateTime To,
    AccountingExportFormat Format = AccountingExportFormat.Csv) : IRequest<Result<byte[]>>;

public sealed class ExportDetailedBalanceQueryHandler
    : IRequestHandler<ExportDetailedBalanceQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportDetailedBalanceQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportDetailedBalanceQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetDetailedBalanceAsync(
            request.AccountFrom, request.AccountTo, request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportDetailedBalanceToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateDetailedBalancePdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    "Balance détaillée",
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportDetailedBalanceToCsv(result.Value)
        };
    }
}

public sealed record ExportPeriodicBalanceQuery(int FiscalYear, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportPeriodicBalanceQueryHandler
    : IRequestHandler<ExportPeriodicBalanceQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportPeriodicBalanceQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportPeriodicBalanceQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetBalanceByPeriodAsync(request.FiscalYear, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportPeriodicBalanceToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GeneratePeriodicBalancePdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    "Balance par période",
                    $"Exercice {request.FiscalYear}"),
                cancellationToken),
            _ => _export.ExportPeriodicBalanceToCsv(result.Value)
        };
    }
}

public sealed record ExportGeneralLedgerQuery(
    string? AccountFrom,
    string? AccountTo,
    DateTime From,
    DateTime To,
    bool IncludeUnmoved = false,
    AccountingExportFormat Format = AccountingExportFormat.Csv) : IRequest<Result<byte[]>>;

public sealed class ExportGeneralLedgerQueryHandler
    : IRequestHandler<ExportGeneralLedgerQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportGeneralLedgerQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportGeneralLedgerQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetLedgerRangeAsync(
            request.AccountFrom, request.AccountTo, request.From, request.To, request.IncludeUnmoved, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        var scope = request.AccountFrom is null && request.AccountTo is null
            ? "tous les comptes"
            : $"du compte {request.AccountFrom ?? "début"} au compte {request.AccountTo ?? "fin"}";

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportGeneralLedgerToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateGeneralLedgerPdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    $"Grand livre général — {scope}",
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportGeneralLedgerToCsv(result.Value)
        };
    }
}

public sealed record ExportLedgerRecapQuery(int Level, DateTime From, DateTime To, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportLedgerRecapQueryHandler
    : IRequestHandler<ExportLedgerRecapQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportLedgerRecapQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportLedgerRecapQuery request, CancellationToken cancellationToken)
    {
        // Le récapitulatif est structurellement une balance agrégée : il réutilise ses rendus.
        var result = await _reporting.GetLedgerRecapAsync(request.Level, request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportBalanceToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateBalancePdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    $"Récapitulatif du grand livre — racine à {request.Level} chiffre(s)",
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportBalanceToCsv(result.Value)
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
