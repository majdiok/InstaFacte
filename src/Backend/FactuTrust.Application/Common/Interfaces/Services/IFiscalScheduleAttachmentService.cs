using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IFiscalScheduleAttachmentService
{
    Task<Result<IReadOnlyList<FiscalScheduleAttachmentDto>>> ListAsync(Guid fiscalScheduleEntryId, CancellationToken cancellationToken = default);

    Task<Result<FiscalScheduleAttachmentDto>> UploadAsync(
        Guid fiscalScheduleEntryId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Result<AttachmentDownload>> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid fiscalScheduleEntryId, Guid attachmentId, CancellationToken cancellationToken = default);
}
