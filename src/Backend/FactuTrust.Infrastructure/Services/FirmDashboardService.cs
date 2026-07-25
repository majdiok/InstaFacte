using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmDashboardService : IFirmDashboardService
{
    private readonly MasterDbContext _masterContext;
    private readonly ITenantService _tenantService;
    private readonly IFirmFiscalOpsAggregator _fiscalOpsAggregator;
    private readonly IFirmFiscalOpsFeature _fiscalOpsFeature;
    private readonly IFirmDossierAccessService _dossierAccess;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<FirmDashboardService> _logger;

    public FirmDashboardService(
        MasterDbContext masterContext,
        ITenantService tenantService,
        IFirmFiscalOpsAggregator fiscalOpsAggregator,
        IFirmFiscalOpsFeature fiscalOpsFeature,
        IFirmDossierAccessService dossierAccess,
        ICurrentUser currentUser,
        ILogger<FirmDashboardService> logger)
    {
        _masterContext = masterContext;
        _tenantService = tenantService;
        _fiscalOpsAggregator = fiscalOpsAggregator;
        _fiscalOpsFeature = fiscalOpsFeature;
        _dossierAccess = dossierAccess;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<FirmDashboardDto> GetDashboardAsync(Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid>? allowedCompanyIds = null;
        if (_currentUser.TryGetAccessScope(out var scope))
        {
            allowedCompanyIds = await _dossierAccess.GetAccessibleCompanyTenantIdsAsync(
                firmTenantId, scope, cancellationToken);
        }
        else if (_currentUser.IsAuthenticated && _currentUser.Role is UserRole.FirmAccountant)
        {
            allowedCompanyIds = new HashSet<Guid>();
        }

        var invitationRows = await (
            from a in _masterContext.FirmClientAssignments.AsNoTracking()
            join t in _masterContext.Tenants.AsNoTracking() on a.CompanyTenantId equals t.Id
            where a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.PendingFirmApproval
            orderby a.RequestedAt descending
            select new FirmDashboardInvitationRowDto
            {
                Id = a.Id,
                CompanyName = t.CompanyName,
                RequestedAt = a.RequestedAt,
                Notes = a.Notes
            })
            .Take(10)
            .ToListAsync(cancellationToken);

        if (allowedCompanyIds is { Count: 0 })
        {
            return new FirmDashboardDto
            {
                PendingInvitationsCount = invitationRows.Count,
                Clients = [],
                PendingInvitations = invitationRows
            };
        }

        var clientsQuery =
            from a in _masterContext.FirmClientAssignments.AsNoTracking()
            join t in _masterContext.Tenants.AsNoTracking() on a.CompanyTenantId equals t.Id
            where a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active
            select new { a, t.CompanyName };

        if (allowedCompanyIds is not null)
            clientsQuery = clientsQuery.Where(c => allowedCompanyIds.Contains(c.a.CompanyTenantId));

        var clients = await clientsQuery.OrderBy(c => c.CompanyName).ToListAsync(cancellationToken);

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var clientRows = new List<FirmDashboardClientRowDto>();
        var inactiveCount = 0;
        var vatDraftsTotal = 0;
        var companyTenantIds = new List<Guid>();

        foreach (var item in clients)
        {
            companyTenantIds.Add(item.a.CompanyTenantId);
            DateTime? lastEntry = null;
            var vatDrafts = 0;
            try
            {
                var conn = await _tenantService.GetConnectionStringAsync(item.a.CompanyTenantId, cancellationToken);
                if (!string.IsNullOrEmpty(conn))
                {
                    await using var ctx = CreateTenantContext(conn);
                    lastEntry = await ctx.JournalEntries.AsNoTracking()
                        .MaxAsync(e => (DateTime?)e.EntryDate, cancellationToken);

                    vatDrafts = await ctx.VatDeclarations.AsNoTracking()
                        .CountAsync(v => v.Status == VatDeclarationStatus.Draft, cancellationToken);
                    vatDraftsTotal += vatDrafts;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to aggregate dashboard metrics for tenant {TenantId}", item.a.CompanyTenantId);
            }

            var isInactive = lastEntry is null || lastEntry < cutoff;
            if (isInactive)
                inactiveCount++;

            clientRows.Add(new FirmDashboardClientRowDto
            {
                AssignmentId = item.a.Id,
                CompanyTenantId = item.a.CompanyTenantId,
                CompanyName = item.CompanyName,
                ActiveSince = item.a.RespondedAt ?? item.a.RequestedAt,
                LastJournalEntryDate = lastEntry,
                IsInactive30Days = isInactive
            });
        }

        var fiscalSummary = _fiscalOpsFeature.IsEnabled && companyTenantIds.Count > 0
            ? await _fiscalOpsAggregator.AggregateAsync(companyTenantIds, cancellationToken)
            : new FirmFiscalOpsSummaryDto();

        return new FirmDashboardDto
        {
            ActiveClientsCount = clientRows.Count,
            PendingInvitationsCount = invitationRows.Count,
            InactiveDossiersCount = inactiveCount,
            VatDraftsCount = vatDraftsTotal,
            OverdueSchedulesCount = fiscalSummary.OverdueSchedulesCount,
            UpcomingWithin7DaysCount = fiscalSummary.UpcomingWithin7DaysCount,
            TejPendingCount = fiscalSummary.TejPendingCount,
            LiasseDraftsCount = fiscalSummary.LiasseDraftsCount,
            DtsPendingCount = fiscalSummary.DtsPendingCount,
            OverdueEstimatedAmount = fiscalSummary.OverdueEstimatedAmount,
            Upcoming7DaysEstimatedAmount = fiscalSummary.Upcoming7DaysEstimatedAmount,
            Clients = clientRows,
            PendingInvitations = invitationRows
        };
    }

    private static TenantDbContext CreateTenantContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new TenantDbContext(options);
    }
}
