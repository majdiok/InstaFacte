using System.Globalization;
using System.Text;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Declarations;

/// <summary>Export CSV récapitulatif des certificats de retenue à la source.</summary>
public sealed record ExportPayrollWithholdingCertificatesCsvQuery(int Year) : IRequest<Result<byte[]>>;

public sealed class ExportPayrollWithholdingCertificatesCsvQueryHandler
    : IRequestHandler<ExportPayrollWithholdingCertificatesCsvQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;

    public ExportPayrollWithholdingCertificatesCsvQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<Result<byte[]>> Handle(
        ExportPayrollWithholdingCertificatesCsvQuery request,
        CancellationToken cancellationToken)
    {
        var batchResult = await _mediator.Send(
            new GeneratePayrollWithholdingCertificatesQuery(request.Year),
            cancellationToken);
        if (batchResult.IsFailure)
            return Result.Failure<byte[]>(batchResult.Error);

        var dto = batchResult.Value;
        var sb = new StringBuilder();
        sb.AppendLine(
            "Matricule;Nom;CIN;CNSS;Mois;Brut;Brut CNSS;CNSS sal.;Frais pro;Déductions famille;Net imposable;IRPP retenu;CSS retenue;Total retenues");

        foreach (var line in dto.Lines)
        {
            sb.Append(EscapeCsv(line.EmployeeNumber)).Append(';');
            sb.Append(EscapeCsv(line.EmployeeName)).Append(';');
            sb.Append(EscapeCsv(line.Cin ?? string.Empty)).Append(';');
            sb.Append(EscapeCsv(line.CnssNumber ?? string.Empty)).Append(';');
            sb.Append(line.MonthsCount.ToString(CultureInfo.InvariantCulture)).Append(';');
            sb.Append(FormatAmount(line.TotalGross)).Append(';');
            sb.Append(FormatAmount(line.TotalCnssableGross)).Append(';');
            sb.Append(FormatAmount(line.TotalCnssEmployee)).Append(';');
            sb.Append(FormatAmount(line.TotalProfessionalExpenses)).Append(';');
            sb.Append(FormatAmount(line.TotalFamilyDeductions)).Append(';');
            sb.Append(FormatAmount(line.AnnualNetTaxable)).Append(';');
            sb.Append(FormatAmount(line.TotalIrppWithheld)).Append(';');
            sb.Append(FormatAmount(line.TotalCssWithheld)).Append(';');
            sb.Append(FormatAmount(line.TotalWithholding));
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append("TOTAL;;;;");
        sb.Append(dto.Lines.Sum(l => l.MonthsCount).ToString(CultureInfo.InvariantCulture)).Append(';');
        sb.Append(FormatAmount(dto.TotalGross)).Append(';');
        sb.Append(';');
        sb.Append(';');
        sb.Append(';');
        sb.Append(';');
        sb.Append(FormatAmount(dto.TotalAnnualNetTaxable)).Append(';');
        sb.Append(FormatAmount(dto.TotalIrppWithheld)).Append(';');
        sb.Append(FormatAmount(dto.TotalCssWithheld)).Append(';');
        sb.Append(FormatAmount(dto.TotalWithholding));

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return Result.Success(bytes);
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.000", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
