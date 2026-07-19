using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

// ── Balance auxiliaire (une ligne par tiers) ─────────────────────────────────────

public sealed record GetAuxiliaryBalanceQuery(ThirdPartyKind Kind, DateTime From, DateTime To)
    : IRequest<Result<IReadOnlyList<AuxiliaryBalanceRowDto>>>;

public sealed class GetAuxiliaryBalanceQueryHandler
    : IRequestHandler<GetAuxiliaryBalanceQuery, Result<IReadOnlyList<AuxiliaryBalanceRowDto>>>
{
    private readonly IAccountingReportingService _reporting;
    public GetAuxiliaryBalanceQueryHandler(IAccountingReportingService reporting) => _reporting = reporting;

    public Task<Result<IReadOnlyList<AuxiliaryBalanceRowDto>>> Handle(GetAuxiliaryBalanceQuery request, CancellationToken cancellationToken)
        => _reporting.GetAuxiliaryBalanceAsync(request.Kind, request.From, request.To, cancellationToken);
}

public sealed record ExportAuxiliaryBalanceCsvQuery(ThirdPartyKind Kind, DateTime From, DateTime To, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportAuxiliaryBalanceCsvQueryHandler
    : IRequestHandler<ExportAuxiliaryBalanceCsvQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportAuxiliaryBalanceCsvQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportAuxiliaryBalanceCsvQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetAuxiliaryBalanceAsync(request.Kind, request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportAuxiliaryBalanceToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateAuxiliaryBalancePdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    $"Balance auxiliaire — {AccountingExportHelpers.KindLabel(request.Kind)}",
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportAuxiliaryBalanceToCsv(result.Value)
        };
    }
}

// ── Grand livre d'un tiers ───────────────────────────────────────────────────────

public sealed record GetThirdPartyLedgerQuery(Guid ThirdPartyId, ThirdPartyKind Kind, DateTime From, DateTime To)
    : IRequest<Result<ThirdPartyLedgerDto>>;

public sealed class GetThirdPartyLedgerQueryHandler
    : IRequestHandler<GetThirdPartyLedgerQuery, Result<ThirdPartyLedgerDto>>
{
    private readonly IAccountingReportingService _reporting;
    public GetThirdPartyLedgerQueryHandler(IAccountingReportingService reporting) => _reporting = reporting;

    public Task<Result<ThirdPartyLedgerDto>> Handle(GetThirdPartyLedgerQuery request, CancellationToken cancellationToken)
        => _reporting.GetThirdPartyLedgerAsync(request.ThirdPartyId, request.Kind, request.From, request.To, cancellationToken);
}

public sealed record ExportThirdPartyLedgerCsvQuery(Guid ThirdPartyId, ThirdPartyKind Kind, DateTime From, DateTime To, AccountingExportFormat Format = AccountingExportFormat.Csv)
    : IRequest<Result<byte[]>>;

public sealed class ExportThirdPartyLedgerCsvQueryHandler
    : IRequestHandler<ExportThirdPartyLedgerCsvQuery, Result<byte[]>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportThirdPartyLedgerCsvQueryHandler(IAccountingReportingService reporting, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reporting = reporting;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportThirdPartyLedgerCsvQuery request, CancellationToken cancellationToken)
    {
        var result = await _reporting.GetThirdPartyLedgerAsync(request.ThirdPartyId, request.Kind, request.From, request.To, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportThirdPartyLedgerToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateThirdPartyLedgerPdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    $"Grand livre tiers — {result.Value.ThirdPartyName}",
                    AccountingExportHelpers.PeriodRange(request.From, request.To)),
                cancellationToken),
            _ => _export.ExportThirdPartyLedgerToCsv(result.Value)
        };
    }
}
