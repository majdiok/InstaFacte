using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Cash operation number generator delegating to unified document numbering.
/// </summary>
public sealed class CashOperationNumberGenerator : ICashOperationNumberGenerator
{
    private readonly IDocumentNumberService _documentNumberService;

    public CashOperationNumberGenerator(IDocumentNumberService documentNumberService)
    {
        _documentNumberService = documentNumberService;
    }

    public async Task<CashOperationNumber> ReserveNextNumberAsync(
        Guid tenantId,
        int fiscalYear,
        CashOperationType operationType,
        CancellationToken cancellationToken = default)
    {
        var docType = operationType == CashOperationType.Credit
            ? NumberingDocumentType.CashReceipt
            : NumberingDocumentType.CashExpense;

        var result = await _documentNumberService.ReserveNextAsync(
            tenantId, docType, fiscalYear, new DateTime(fiscalYear, 1, 1), cancellationToken);

        var numberResult = DocumentNumberMapper.ToCashOperationNumber(result);
        if (numberResult.IsFailure)
            throw new InvalidOperationException(numberResult.Error.Description);

        return numberResult.Value;
    }

    public Task<string> PreviewNextNumberAsync(
        Guid tenantId,
        int fiscalYear,
        CashOperationType operationType,
        CancellationToken cancellationToken = default)
    {
        var docType = operationType == CashOperationType.Credit
            ? NumberingDocumentType.CashReceipt
            : NumberingDocumentType.CashExpense;

        return _documentNumberService.PreviewNextAsync(
            tenantId, docType, fiscalYear, new DateTime(fiscalYear, 1, 1), cancellationToken);
    }
}
