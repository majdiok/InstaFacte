using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface ITejXmlGeneratorService
{
    Task<TejXmlExportResultDto> GenerateAsync(
        Company declarant,
        IReadOnlyList<TejRsCertificatPayload> certificats,
        int year,
        int month,
        TejSubmissionType submissionType,
        CancellationToken ct = default);
}
