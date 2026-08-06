using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Reports;

/// <summary>Export du journal de paie (vue par salarié ou ventilation comptable) en PDF, Excel ou CSV.</summary>
public sealed record ExportPayrollJournalQuery(
    int Year,
    int Month,
    bool IncludeCalculated = false,
    AccountingExportFormat Format = AccountingExportFormat.Csv,
    PayrollJournalView View = PayrollJournalView.ByEmployee) : IRequest<Result<PayrollReportFileDto>>;

public sealed class ExportPayrollJournalQueryHandler
    : IRequestHandler<ExportPayrollJournalQuery, Result<PayrollReportFileDto>>
{
    private readonly IMediator _mediator;
    private readonly IPayrollReportExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportPayrollJournalQueryHandler(
        IMediator mediator,
        IPayrollReportExportService export,
        IPdfService pdf,
        ICompanyRepository companies)
    {
        _mediator = mediator;
        _export = export;
        _pdf = pdf;
        _companies = companies;
    }

    public async Task<Result<PayrollReportFileDto>> Handle(ExportPayrollJournalQuery request, CancellationToken cancellationToken)
    {
        var generated = await _mediator.Send(
            new GeneratePayrollJournalQuery(request.Year, request.Month, request.IncludeCalculated),
            cancellationToken);

        if (generated.IsFailure)
            return Result.Failure<PayrollReportFileDto>(generated.Error);

        var journal = generated.Value;
        var title = request.View == PayrollJournalView.Accounting
            ? "Journal de paie — ventilation comptable"
            : "Journal de paie";

        var content = request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportPayrollJournalToExcel(journal, request.View),
            AccountingExportFormat.Pdf => await _pdf.GeneratePayrollJournalPdfAsync(
                journal,
                request.View,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    title,
                    journal.PeriodLabel),
                cancellationToken),
            _ => _export.ExportPayrollJournalToCsv(journal, request.View)
        };

        var (extension, contentType) = PayrollReportHelpers.FileMeta(request.Format);
        var viewSuffix = request.View == PayrollJournalView.Accounting ? "_comptable" : string.Empty;
        var provisional = journal.IsProvisional ? "_provisoire" : string.Empty;
        var fileName = $"journal_paie_{journal.Year}_{journal.Month:D2}{viewSuffix}{provisional}.{extension}";

        return Result.Success(new PayrollReportFileDto(content, fileName, contentType));
    }
}
