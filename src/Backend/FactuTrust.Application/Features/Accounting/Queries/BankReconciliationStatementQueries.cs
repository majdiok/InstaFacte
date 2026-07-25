using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

/// <summary>État de rapprochement d'un relevé (lecture seule).</summary>
public sealed record GetBankReconciliationStatementQuery(Guid StatementId)
    : IRequest<Result<BankReconciliationStatementDto>>;

public sealed class GetBankReconciliationStatementQueryHandler
    : IRequestHandler<GetBankReconciliationStatementQuery, Result<BankReconciliationStatementDto>>
{
    private readonly IBankReconciliationService _reconciliation;

    public GetBankReconciliationStatementQueryHandler(IBankReconciliationService reconciliation)
    {
        _reconciliation = reconciliation;
    }

    public Task<Result<BankReconciliationStatementDto>> Handle(GetBankReconciliationStatementQuery request, CancellationToken cancellationToken)
        => _reconciliation.GetReconciliationStatementAsync(request.StatementId, cancellationToken);
}

/// <summary>Export de l'état de rapprochement (CSV / Excel / PDF).</summary>
public sealed record ExportBankReconciliationStatementQuery(
    Guid StatementId,
    AccountingExportFormat Format = AccountingExportFormat.Csv) : IRequest<Result<byte[]>>;

public sealed class ExportBankReconciliationStatementQueryHandler
    : IRequestHandler<ExportBankReconciliationStatementQuery, Result<byte[]>>
{
    private readonly IBankReconciliationService _reconciliation;
    private readonly IAccountingExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportBankReconciliationStatementQueryHandler(
        IBankReconciliationService reconciliation, IAccountingExportService export, IPdfService pdf, ICompanyRepository companies)
    {
        _reconciliation = reconciliation;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<byte[]>> Handle(ExportBankReconciliationStatementQuery request, CancellationToken cancellationToken)
    {
        var result = await _reconciliation.GetReconciliationStatementAsync(request.StatementId, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<byte[]>(result.Error);

        return request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportBankReconciliationStatementToExcel(result.Value),
            AccountingExportFormat.Pdf => await _pdf.GenerateBankReconciliationStatementPdfAsync(
                result.Value,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    "État de rapprochement bancaire",
                    AccountingExportHelpers.PeriodRange(result.Value.PeriodStart, result.Value.PeriodEnd)),
                cancellationToken),
            _ => _export.ExportBankReconciliationStatementToCsv(result.Value)
        };
    }
}
