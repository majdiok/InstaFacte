using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Payroll.Declarations.CnssRemittance;

public sealed record GenerateCnssRemittanceQuery(int Year, int Month) : IRequest<Result<CnssContributionRemittanceDto>>;

public sealed class GenerateCnssRemittanceQueryHandler
    : IRequestHandler<GenerateCnssRemittanceQuery, Result<CnssContributionRemittanceDto>>
{
    private readonly CnssRemittanceDataLoader _loader;
    private readonly AccountingSettings _settings;

    public GenerateCnssRemittanceQueryHandler(
        CnssRemittanceDataLoader loader,
        IOptions<AccountingSettings> settings)
    {
        _loader = loader;
        _settings = settings.Value;
    }

    public async Task<Result<CnssContributionRemittanceDto>> Handle(
        GenerateCnssRemittanceQuery request,
        CancellationToken cancellationToken)
    {
        var guard = CnssRemittanceFeatureGuard.EnsureEnabled(_settings);
        if (guard.IsFailure)
            return Result.Failure<CnssContributionRemittanceDto>(guard.Error);

        var dtoResult = await _loader.LoadDtoAsync(request.Year, request.Month, cancellationToken);
        if (dtoResult.IsFailure)
            return dtoResult;

        var dto = dtoResult.Value;
        if (dto.IsEligible && string.IsNullOrWhiteSpace(dto.EmployerCnssNumber))
        {
            return Result.Failure<CnssContributionRemittanceDto>(Error.Validation(
                "CnssEmployerNumber",
                "Le matricule employeur CNSS est obligatoire pour générer le bordereau."));
        }

        return dtoResult;
    }
}

public sealed record GetCnssRemittancePaymentQuery(int Year, int Month) : IRequest<Result<CnssContributionRemittanceDto>>;

public sealed class GetCnssRemittancePaymentQueryHandler
    : IRequestHandler<GetCnssRemittancePaymentQuery, Result<CnssContributionRemittanceDto>>
{
    private readonly CnssRemittanceDataLoader _loader;
    private readonly AccountingSettings _settings;

    public GetCnssRemittancePaymentQueryHandler(
        CnssRemittanceDataLoader loader,
        IOptions<AccountingSettings> settings)
    {
        _loader = loader;
        _settings = settings.Value;
    }

    public Task<Result<CnssContributionRemittanceDto>> Handle(
        GetCnssRemittancePaymentQuery request,
        CancellationToken cancellationToken)
    {
        var guard = CnssRemittanceFeatureGuard.EnsureEnabled(_settings);
        if (guard.IsFailure)
            return Task.FromResult(Result.Failure<CnssContributionRemittanceDto>(guard.Error));

        return _loader.LoadDtoAsync(request.Year, request.Month, cancellationToken);
    }
}
