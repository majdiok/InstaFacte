using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmFiscalScheduleWriteService
{
    Task<IReadOnlyList<FirmClientDossierDto>> GetCompaniesAsync(Guid firmTenantId, CancellationToken cancellationToken = default);

    Task<Result<int>> EnsureFiscalYearAsync(Guid firmTenantId, Guid companyTenantId, int fiscalYear, CancellationToken cancellationToken = default);

    Task<Result<FiscalScheduleEntryDto>> CreateEntryAsync(Guid firmTenantId, Guid companyTenantId, CreateFiscalScheduleEntryRequest request, CancellationToken cancellationToken = default);

    Task<Result<FiscalScheduleEntryDto>> UpdateEntryAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, UpdateFiscalScheduleEntryRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteEntryAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, CancellationToken cancellationToken = default);

    Task<Result<FiscalScheduleEntryDto>> MarkDepositedAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, MarkFiscalScheduleDepositedRequest request, CancellationToken cancellationToken = default);

    Task<Result<FiscalScheduleEntryDto>> CapturePaymentAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, CaptureFiscalSchedulePaymentRequest request, CancellationToken cancellationToken = default);

    Task<Result<FiscalScheduleEntryDto>> MarkValidatedAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, MarkFiscalScheduleValidatedRequest request, CancellationToken cancellationToken = default);

    Task<Result<FiscalScheduleEntryDto>> ScheduleReminderAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, ScheduleFiscalReminderRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FiscalScheduleHistoryDto>>> GetHistoryAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FiscalScheduleAttachmentDto>>> GetAttachmentsAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, CancellationToken cancellationToken = default);

    Task<Result<FiscalScheduleAttachmentDto>> UploadAttachmentAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

    Task<Result<AttachmentDownload>> DownloadAttachmentAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, Guid attachmentId, CancellationToken cancellationToken = default);

    Task<Result> DeleteAttachmentAsync(Guid firmTenantId, Guid companyTenantId, Guid entryId, Guid attachmentId, CancellationToken cancellationToken = default);
}
