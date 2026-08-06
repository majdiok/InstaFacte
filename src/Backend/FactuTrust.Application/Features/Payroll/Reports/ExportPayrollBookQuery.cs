using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Reports;

/// <summary>Export du livre de paie simplifié (PDF, Excel ou CSV).</summary>
public sealed record ExportPayrollBookQuery(
    int Year,
    int FromMonth,
    int ToMonth,
    bool IncludeCalculated = false,
    AccountingExportFormat Format = AccountingExportFormat.Csv) : IRequest<Result<PayrollReportFileDto>>;

public sealed class ExportPayrollBookQueryHandler
    : IRequestHandler<ExportPayrollBookQuery, Result<PayrollReportFileDto>>
{
    private readonly IMediator _mediator;
    private readonly IPayrollReportExportService _export;
    private readonly IPdfService _pdf;
    private readonly ICompanyRepository _companies;

    public ExportPayrollBookQueryHandler(
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

    public async Task<Result<PayrollReportFileDto>> Handle(ExportPayrollBookQuery request, CancellationToken cancellationToken)
    {
        var generated = await _mediator.Send(
            new GeneratePayrollBookQuery(request.Year, request.FromMonth, request.ToMonth, request.IncludeCalculated),
            cancellationToken);

        if (generated.IsFailure)
            return Result.Failure<PayrollReportFileDto>(generated.Error);

        var book = generated.Value;

        var content = request.Format switch
        {
            AccountingExportFormat.Excel => _export.ExportPayrollBookToExcel(book),
            AccountingExportFormat.Pdf => await _pdf.GeneratePayrollBookPdfAsync(
                book,
                AccountingExportHelpers.Header(
                    await _companies.GetDefaultAsync(cancellationToken),
                    "Livre de paie simplifié",
                    book.PeriodLabel),
                cancellationToken),
            _ => _export.ExportPayrollBookToCsv(book)
        };

        var (extension, contentType) = PayrollReportHelpers.FileMeta(request.Format);
        var provisional = book.IsProvisional ? "_provisoire" : string.Empty;
        var fileName = $"livre_paie_{book.Year}_{book.FromMonth:D2}-{book.ToMonth:D2}{provisional}.{extension}";

        return Result.Success(new PayrollReportFileDto(content, fileName, contentType));
    }
}
