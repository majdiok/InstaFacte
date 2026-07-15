using System.Globalization;
using System.Text;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Declarations;

/// <summary>
/// Export CSV de la déclaration trimestrielle des salaires (DTS CNSS).
/// </summary>
public sealed record ExportDtsDeclarationQuery(int Year, int Quarter) : IRequest<Result<byte[]>>;

public sealed class ExportDtsDeclarationQueryHandler : IRequestHandler<ExportDtsDeclarationQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;

    public ExportDtsDeclarationQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<Result<byte[]>> Handle(ExportDtsDeclarationQuery request, CancellationToken cancellationToken)
    {
        var declaration = await _mediator.Send(new GenerateDtsDeclarationQuery(request.Year, request.Quarter), cancellationToken);
        if (declaration.IsFailure)
            return Result.Failure<byte[]>(declaration.Error);

        var dto = declaration.Value;
        var sb = new StringBuilder();
        sb.AppendLine("Nom;CNSS;Brut total;Brut CNSS;CNSS salarié;CNSS patronale;Mois");

        foreach (var line in dto.Lines)
        {
            sb.Append(EscapeCsv(line.EmployeeName)).Append(';');
            sb.Append(EscapeCsv(line.CnssNumber ?? string.Empty)).Append(';');
            sb.Append(FormatAmount(line.TotalGross)).Append(';');
            sb.Append(FormatAmount(line.TotalCnssableGross)).Append(';');
            sb.Append(FormatAmount(line.CnssEmployee)).Append(';');
            sb.Append(FormatAmount(line.CnssEmployer)).Append(';');
            sb.Append(line.MonthsCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append("TOTAL;;;;");
        sb.Append(FormatAmount(dto.TotalCnssableGross)).Append(';');
        sb.Append(FormatAmount(dto.TotalCnssEmployee)).Append(';');
        sb.Append(FormatAmount(dto.TotalCnssEmployer)).Append(';');
        sb.Append(dto.EmployeeCount.ToString(CultureInfo.InvariantCulture));

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
