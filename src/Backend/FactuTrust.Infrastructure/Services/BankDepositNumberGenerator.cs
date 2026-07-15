using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Bank deposit number generator delegating to unified document numbering.
/// </summary>
public sealed class BankDepositNumberGenerator : IBankDepositNumberGenerator
{
    private readonly IDocumentNumberService _documentNumberService;

    public BankDepositNumberGenerator(IDocumentNumberService documentNumberService)
    {
        _documentNumberService = documentNumberService;
    }

    public async Task<BankDepositNumber> ReserveNextNumberAsync(
        Guid tenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var result = await _documentNumberService.ReserveNextAsync(
            tenantId,
            NumberingDocumentType.BankDeposit,
            fiscalYear,
            new DateTime(fiscalYear, 1, 1),
            cancellationToken);

        var numberResult = DocumentNumberMapper.ToBankDepositNumber(result);
        if (numberResult.IsFailure)
            throw new InvalidOperationException(numberResult.Error.Description);

        return numberResult.Value;
    }

    public Task<string> PreviewNextNumberAsync(
        Guid tenantId,
        int fiscalYear,
        CancellationToken cancellationToken = default) =>
        _documentNumberService.PreviewNextAsync(
            tenantId,
            NumberingDocumentType.BankDeposit,
            fiscalYear,
            new DateTime(fiscalYear, 1, 1),
            cancellationToken);
}
