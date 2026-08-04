using FactuTrust.Application.Common;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

public interface IExchangeService
{
    Task<IReadOnlyList<ExchangeThreadListItemDto>> ListThreadsAsync(
        Guid homeTenantId,
        TenantKind tenantKind,
        FirmDossierAccessScope? firmScope,
        Guid? viewerUserId = null,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeThreadDetailDto>> GetThreadAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeThreadDetailDto>> EnsureThreadAsync(
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        Guid? firmClientAssignmentId,
        CancellationToken cancellationToken = default);

    Task<Result> CloseThreadAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result> ReopenThreadAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result<PagedExchangeMessagesDto>> GetMessagesAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        DateTime? after,
        DateTime? before = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeMessageDto>> SendMessageAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        SendExchangeMessageDto dto,
        CancellationToken cancellationToken = default);

    Task<Result> MarkMessageReadAsync(
        Guid threadId,
        Guid messageId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result> MarkMessagesReadBatchAsync(
        Guid threadId,
        IReadOnlyList<Guid> messageIds,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ExchangeRequestDto>>> ListRequestsAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeRequestDto>> CreateRequestAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CreateExchangeRequestDto dto,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeRequestDto>> ChangeRequestStatusAsync(
        Guid threadId,
        Guid requestId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        ChangeExchangeRequestStatusDto dto,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeRequestDto>> AssignRequestAsync(
        Guid threadId,
        Guid requestId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        AssignExchangeRequestDto dto,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ExchangeTaskDto>>> ListTasksAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeTaskDto>> CreateTaskAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CreateExchangeTaskDto dto,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeTaskDto>> ChangeTaskStatusAsync(
        Guid threadId,
        Guid taskId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        ChangeExchangeTaskStatusDto dto,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ExchangeDocumentDto>>> ListDocumentsAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeDocumentDto>> UploadDocumentAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        string fileName,
        string contentType,
        Stream content,
        Guid? messageId,
        Guid? requestId,
        Guid? taskId,
        CancellationToken cancellationToken = default);

    Task<Result<(Stream Stream, string FileName, string ContentType)>> DownloadDocumentAsync(
        Guid threadId,
        Guid documentId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteDocumentAsync(
        Guid threadId,
        Guid documentId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ExchangeAuditEventDto>>> GetHistoryAsync(
        Guid threadId,
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ExchangeUnreadSummaryDto> GetUnreadSummaryAsync(
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        CancellationToken cancellationToken = default);

    Task<Result<ExchangeBootstrapDto>> BootstrapAsync(
        Guid homeTenantId,
        TenantKind tenantKind,
        Guid userId,
        string displayName,
        string? userRole,
        FirmDossierAccessScope? firmScope,
        Guid? threadId,
        string? tab,
        CancellationToken cancellationToken = default);
}
