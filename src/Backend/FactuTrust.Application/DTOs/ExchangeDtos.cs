using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record ExchangeThreadListItemDto(
    Guid Id,
    Guid FirmClientAssignmentId,
    Guid FirmTenantId,
    Guid CompanyTenantId,
    string CounterpartName,
    ExchangeThreadStatus Status,
    DateTime? LastActivityAt,
    int UnreadCount,
    string? Subject);

public sealed record ExchangeParticipantDto(
    Guid UserId,
    string DisplayName,
    string Role,
    string Side,
    bool IsOnline);

public sealed record ExchangeThreadDetailDto(
    Guid Id,
    Guid FirmClientAssignmentId,
    Guid FirmTenantId,
    Guid CompanyTenantId,
    string CompanyName,
    string FirmName,
    ExchangeThreadStatus Status,
    string? Subject,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    DateTime? ClosedAt,
    Guid? ClosedByUserId,
    IReadOnlyList<ExchangeParticipantDto> Participants);

public sealed record ExchangeMessageReadDto(Guid UserId, string? DisplayName, DateTime ReadAt);

public sealed record ExchangeMessageDto(
    Guid Id,
    Guid ThreadId,
    Guid AuthorUserId,
    Guid AuthorTenantId,
    string AuthorDisplayName,
    ExchangeMessageVisibility Visibility,
    string Body,
    DateTime SentAt,
    IReadOnlyList<ExchangeMessageReadDto> ReadReceipts,
    IReadOnlyList<ExchangeDocumentDto> Attachments);

public sealed record SendExchangeMessageDto(
    string Body,
    ExchangeMessageVisibility Visibility = ExchangeMessageVisibility.ClientVisible);

public sealed record ExchangeRequestDto(
    Guid Id,
    Guid ThreadId,
    int Number,
    string Title,
    string Description,
    ExchangeRequestCategory Category,
    ExchangeRequestPriority Priority,
    ExchangeRequestStatus Status,
    Guid CreatedByUserId,
    Guid CreatedByTenantId,
    Guid? AssigneeUserId,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt);

public sealed record CreateExchangeRequestDto(
    string Title,
    string? Description,
    ExchangeRequestCategory Category,
    ExchangeRequestPriority Priority = ExchangeRequestPriority.Normal);

public sealed record ChangeExchangeRequestStatusDto(ExchangeRequestStatus Status);

public sealed record AssignExchangeRequestDto(Guid AssigneeUserId);

public sealed record ExchangeRequestCommentDto(
    Guid Id,
    Guid ThreadId,
    Guid RequestId,
    Guid AuthorUserId,
    Guid AuthorTenantId,
    string AuthorDisplayName,
    string Body,
    DateTime CreatedAt);

public sealed record CreateExchangeRequestCommentDto(string Body);

public sealed record ExchangeTaskDto(
    Guid Id,
    Guid ThreadId,
    string Title,
    string? Description,
    DateTime? DueDate,
    Guid? AssigneeUserId,
    Guid? AssigneeTenantId,
    ExchangeTaskStatus Status,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public sealed record CreateExchangeTaskDto(
    string Title,
    string? Description,
    DateTime? DueDate,
    Guid? AssigneeUserId);

public sealed record ChangeExchangeTaskStatusDto(ExchangeTaskStatus Status);

public sealed record ExchangeDocumentDto(
    Guid Id,
    Guid ThreadId,
    Guid? MessageId,
    Guid? RequestId,
    Guid? TaskId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    DateTime UploadedAt);

public sealed record ExchangeAuditEventDto(
    Guid Id,
    Guid ThreadId,
    DateTime OccurredAt,
    Guid ActorUserId,
    string ActorDisplayName,
    ExchangeAuditEventType EventType,
    string? PayloadJson);

public sealed record ExchangeUnreadSummaryDto(
    int TotalUnreadMessages,
    int OpenRequests,
    IReadOnlyList<ExchangeThreadUnreadDto> Threads);

public sealed record ExchangeThreadUnreadDto(Guid ThreadId, int UnreadCount);

public sealed record EnsureExchangeThreadDto(Guid? FirmClientAssignmentId);

public sealed record PagedExchangeMessagesDto(
    IReadOnlyList<ExchangeMessageDto> Items,
    bool HasMore,
    DateTime? OldestSentAt);

public sealed record MarkMessagesReadBatchDto(IReadOnlyList<Guid> MessageIds);

public sealed record ExchangeBootstrapDto(
    IReadOnlyList<ExchangeThreadListItemDto>? Threads,
    FirmClientAssignmentDto? CompanyAssignment,
    IReadOnlyList<FirmClientDossierDto>? FirmClients,
    ExchangeThreadDetailDto? ActiveThread,
    PagedExchangeMessagesDto? Messages,
    IReadOnlyList<ExchangeRequestDto>? Requests,
    IReadOnlyList<ExchangeTaskDto>? Tasks,
    IReadOnlyList<ExchangeDocumentDto>? Documents,
    IReadOnlyList<ExchangeAuditEventDto>? History,
    int OpenRequestsCount,
    int UnreadCount,
    string? EmptyHint);
