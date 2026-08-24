using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Invoices.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IClientPortalService
{
    Task<Result<IReadOnlyList<ClientPortalContactDto>>> ListContactsAsync(Guid clientId, CancellationToken cancellationToken = default);

    Task<Result<ClientPortalContactDto>> InviteAsync(Guid clientId, InviteClientPortalContactRequest request, CancellationToken cancellationToken = default);

    Task<Result> ResendInviteAsync(Guid clientId, Guid contactId, CancellationToken cancellationToken = default);

    Task<Result> RevokeAsync(Guid clientId, Guid contactId, CancellationToken cancellationToken = default);

    Task<Result<AuthResponseDto>> AcceptInviteAsync(AcceptPortalInviteRequest request, CancellationToken cancellationToken = default);

    Task<Result> EnsurePortalLoginAllowedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<PortalMeDto>> GetMeAsync(CancellationToken cancellationToken = default);

    Task<Result<PortalSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<Result<PagedResult<PortalInvoiceListItemDto>>> GetInvoicesAsync(
        InvoiceStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        bool unpaidOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<PortalInvoiceDetailDto>> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<Result<InvoicePdfResult>> GetInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PortalPaymentDto>>> GetPaymentsAsync(
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    Task<Result<PortalStatementDto>> GetStatementAsync(
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    Task<Result> SetPortalEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task<bool> HasActiveContactAsync(Guid clientId, CancellationToken cancellationToken = default);
}
