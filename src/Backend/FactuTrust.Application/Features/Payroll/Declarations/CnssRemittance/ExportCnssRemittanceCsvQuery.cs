using System.Globalization;
using System.Text;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Declarations.CnssRemittance;

public sealed record ExportCnssRemittanceCsvQuery(int Year, int Month) : IRequest<Result<byte[]>>;

public sealed class ExportCnssRemittanceCsvQueryHandler : IRequestHandler<ExportCnssRemittanceCsvQuery, Result<byte[]>>
{
    private readonly CnssRemittanceDataLoader _loader;
    private readonly AccountingSettings _settings;

    public ExportCnssRemittanceCsvQueryHandler(
        CnssRemittanceDataLoader loader,
        IOptions<AccountingSettings> settings)
    {
        _loader = loader;
        _settings = settings.Value;
    }

    public async Task<Result<byte[]>> Handle(ExportCnssRemittanceCsvQuery request, CancellationToken cancellationToken)
    {
        var guard = CnssRemittanceFeatureGuard.EnsureEnabled(_settings);
        if (guard.IsFailure)
            return Result.Failure<byte[]>(guard.Error);

        var dtoResult = await _loader.LoadDtoAsync(request.Year, request.Month, cancellationToken);
        if (dtoResult.IsFailure)
            return Result.Failure<byte[]>(dtoResult.Error);

        var dto = dtoResult.Value;
        if (!dto.IsEligible)
        {
            return Result.Failure<byte[]>(Error.Validation(
                "Period",
                "Le cycle de paie doit être validé ou clôturé pour exporter le bordereau CNSS."));
        }

        if (string.IsNullOrWhiteSpace(dto.EmployerCnssNumber))
        {
            return Result.Failure<byte[]>(Error.Validation(
                "CnssEmployerNumber",
                "Le matricule employeur CNSS est obligatoire pour exporter le bordereau."));
        }

        var sb = new StringBuilder();
        sb.AppendLine("Matricule CNSS salarié;Nom;Brut CNSS;CNSS salarié;CNSS patronale;Accident travail;Total ligne");

        foreach (var line in dto.Lines)
        {
            sb.Append(EscapeCsv(line.CnssNumber ?? string.Empty)).Append(';');
            sb.Append(EscapeCsv(line.EmployeeName)).Append(';');
            sb.Append(FormatAmount(line.CnssableGross)).Append(';');
            sb.Append(FormatAmount(line.CnssEmployee)).Append(';');
            sb.Append(FormatAmount(line.CnssEmployer)).Append(';');
            sb.Append(FormatAmount(line.WorkAccident)).Append(';');
            sb.Append(FormatAmount(line.LineTotal));
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append("Matricule employeur CNSS;Raison sociale;NIF;Période;Effectif;Total CNSS sal.;Total CNSS pat.;Total AT;TOTAL À VERSER").AppendLine();
        sb.Append(EscapeCsv(dto.EmployerCnssNumber ?? string.Empty)).Append(';');
        sb.Append(EscapeCsv(dto.EmployerCompanyName)).Append(';');
        sb.Append(EscapeCsv(dto.EmployerNif)).Append(';');
        sb.Append($"{dto.Month:D2}/{dto.Year}").Append(';');
        sb.Append(dto.EmployeeCount.ToString(CultureInfo.InvariantCulture)).Append(';');
        sb.Append(FormatAmount(dto.TotalCnssEmployee)).Append(';');
        sb.Append(FormatAmount(dto.TotalCnssEmployer)).Append(';');
        sb.Append(FormatAmount(dto.TotalWorkAccident)).Append(';');
        sb.Append(FormatAmount(dto.TotalDue));

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
