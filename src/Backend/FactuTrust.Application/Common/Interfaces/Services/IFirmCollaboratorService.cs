using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IFirmCollaboratorService
{
    Task<Result<IReadOnlyList<FirmUserDto>>> ListAsync(
        Guid firmTenantId,
        string? name,
        string? qualification,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<Result<FirmUserDto>> GetByIdAsync(Guid firmTenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<FirmUserDto>> CreateAsync(
        Guid firmTenantId,
        CreateFirmUserDto dto,
        Stream? cniContent,
        string? cniFileName,
        string? cniContentType,
        CancellationToken cancellationToken = default);

    Task<Result<FirmUserDto>> UpdateAsync(
        Guid firmTenantId,
        Guid userId,
        Guid currentUserId,
        UpdateFirmUserDto dto,
        CancellationToken cancellationToken = default);

    Task<Result> SetActiveAsync(
        Guid firmTenantId,
        Guid userId,
        Guid currentUserId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<Result> ResendInviteAsync(Guid firmTenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result> SetBinomesAsync(
        Guid firmTenantId,
        Guid userId,
        IReadOnlyList<Guid> binomeUserIds,
        CancellationToken cancellationToken = default);

    Task<Result<FirmCollaboratorCniInfoDto>> UploadCniAsync(
        Guid firmTenantId,
        Guid userId,
        Stream content,
        string fileName,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<Result<(Stream Stream, string FileName, string ContentType)>> DownloadCniAsync(
        Guid firmTenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteCniAsync(Guid firmTenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<(byte[] Content, string FileName)>> ExportExcelAsync(
        Guid firmTenantId,
        string? name,
        string? qualification,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<Result<FirmAddressSnapshotDto>> GetFirmAddressAsync(Guid firmTenantId, CancellationToken cancellationToken = default);
}
