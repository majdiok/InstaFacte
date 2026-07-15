using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services;

public interface IDocumentNumberService
{
    Task<DocumentNumberResult> ReserveNextAsync(
        Guid tenantId,
        NumberingDocumentType documentType,
        int fiscalYear,
        DateTime referenceDate,
        CancellationToken cancellationToken = default);

    Task<string> PreviewNextAsync(
        Guid tenantId,
        NumberingDocumentType documentType,
        int fiscalYear,
        DateTime referenceDate,
        CancellationToken cancellationToken = default);

    Task<int> GetEffectiveCurrentSequenceAsync(
        Guid tenantId,
        NumberingDocumentType documentType,
        int fiscalYear,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentNumberResult(
    string Value,
    int Year,
    int Sequence,
    string? Prefix);