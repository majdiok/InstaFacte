using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

public interface ICompanyProfileSnapshotProvider
{
    Task<CompanyProfileSnapshotDto> CaptureForCompanyTenantAsync(Guid companyTenantId, CancellationToken cancellationToken = default);

    CompanyProfileSnapshotDto? TryDeserialize(string? json);

    string Serialize(CompanyProfileSnapshotDto snapshot);
}
