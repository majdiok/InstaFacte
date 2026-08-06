using System.IO.Compression;
using System.Text;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Declarations;

/// <summary>Export ZIP : un PDF par salarié + CSV récapitulatif.</summary>
public sealed record ExportPayrollWithholdingCertificatesZipQuery(int Year) : IRequest<Result<byte[]>>;

public sealed class ExportPayrollWithholdingCertificatesZipQueryHandler
    : IRequestHandler<ExportPayrollWithholdingCertificatesZipQuery, Result<byte[]>>
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;

    public ExportPayrollWithholdingCertificatesZipQueryHandler(IMediator mediator, IPdfService pdf)
    {
        _mediator = mediator;
        _pdf = pdf;
    }

    public async Task<Result<byte[]>> Handle(
        ExportPayrollWithholdingCertificatesZipQuery request,
        CancellationToken cancellationToken)
    {
        var batchResult = await _mediator.Send(
            new GeneratePayrollWithholdingCertificatesQuery(request.Year),
            cancellationToken);
        if (batchResult.IsFailure)
            return Result.Failure<byte[]>(batchResult.Error);

        var batch = batchResult.Value;
        if (batch.EmployeeCount == 0)
        {
            return Result.Failure<byte[]>(Error.Validation(
                "Certificates",
                "Aucun certificat à exporter pour cet exercice."));
        }

        var csvResult = await _mediator.Send(
            new ExportPayrollWithholdingCertificatesCsvQuery(request.Year),
            cancellationToken);
        if (csvResult.IsFailure)
            return Result.Failure<byte[]>(csvResult.Error);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var recapEntry = zip.CreateEntry("_recap.csv", CompressionLevel.Optimal);
            await using (var recapStream = recapEntry.Open())
            {
                await recapStream.WriteAsync(csvResult.Value, cancellationToken);
            }

            foreach (var line in batch.Lines)
            {
                var pdfBytes = await _pdf.GeneratePayrollWithholdingCertificatePdfAsync(
                    line, batch, cancellationToken);
                if (pdfBytes.Length == 0)
                    continue;

                var fileName = BuildPdfFileName(line, request.Year);
                var entry = zip.CreateEntry(fileName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdfBytes, cancellationToken);
            }
        }

        return Result.Success(ms.ToArray());
    }

    public static string BuildPdfFileName(PayrollWithholdingCertificateLineDto line, int year)
    {
        var parts = line.EmployeeName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var lastName = parts.Length > 0 ? parts[0] : "Salarie";
        var firstName = parts.Length > 1 ? parts[1] : string.Empty;

        var raw = $"{line.EmployeeNumber}_{lastName}_{firstName}_CRS_{year}.pdf";
        return SanitizeFileName(raw);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString();
    }
}
