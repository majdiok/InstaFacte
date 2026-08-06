using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Declarations.CnssRemittance;

public sealed record ExportCnssRemittancePdfQuery(int Year, int Month) : IRequest<Result<byte[]>>;

public sealed class ExportCnssRemittancePdfQueryHandler : IRequestHandler<ExportCnssRemittancePdfQuery, Result<byte[]>>
{
    private readonly CnssRemittanceDataLoader _loader;
    private readonly IPdfService _pdf;
    private readonly AccountingSettings _settings;

    public ExportCnssRemittancePdfQueryHandler(
        CnssRemittanceDataLoader loader,
        IPdfService pdf,
        IOptions<AccountingSettings> settings)
    {
        _loader = loader;
        _pdf = pdf;
        _settings = settings.Value;
    }

    public async Task<Result<byte[]>> Handle(ExportCnssRemittancePdfQuery request, CancellationToken cancellationToken)
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

        var bytes = await _pdf.GenerateCnssContributionRemittancePdfAsync(dto, cancellationToken);
        return Result.Success(bytes);
    }
}
