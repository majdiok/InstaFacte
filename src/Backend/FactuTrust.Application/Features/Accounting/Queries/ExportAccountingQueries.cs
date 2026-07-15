using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record ExportJournalCsvQuery(string? JournalCode, DateTime From, DateTime To)
    : IRequest<Result<byte[]>>;

public sealed class ExportJournalCsvQueryHandler
    : IRequestHandler<ExportJournalCsvQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;

    public ExportJournalCsvQueryHandler(IAccountingReportingService reporting, IAccountingExportService export)
    {
        _reporting = reporting;
        _export = export;
    }

    public async Task<Result<byte[]>> Handle(ExportJournalCsvQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetJournalEntriesAsync(request.JournalCode, request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return _export.ExportJournalToCsv(result.Value);
    }
}

public sealed record ExportLedgerCsvQuery(string AccountNumber, DateTime From, DateTime To)
    : IRequest<Result<byte[]>>;

public sealed class ExportLedgerCsvQueryHandler
    : IRequestHandler<ExportLedgerCsvQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;

    public ExportLedgerCsvQueryHandler(IAccountingReportingService reporting, IAccountingExportService export)
    {
        _reporting = reporting;
        _export = export;
    }

    public async Task<Result<byte[]>> Handle(ExportLedgerCsvQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetLedgerAsync(request.AccountNumber, request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return _export.ExportLedgerToCsv(result.Value);
    }
}

public sealed record ExportBalanceCsvQuery(DateTime From, DateTime To)
    : IRequest<Result<byte[]>>;

public sealed class ExportBalanceCsvQueryHandler
    : IRequestHandler<ExportBalanceCsvQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;

    public ExportBalanceCsvQueryHandler(IAccountingReportingService reporting, IAccountingExportService export)
    {
        _reporting = reporting;
        _export = export;
    }

    public async Task<Result<byte[]>> Handle(ExportBalanceCsvQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetBalanceAsync(request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return _export.ExportBalanceToCsv(result.Value);
    }
}
