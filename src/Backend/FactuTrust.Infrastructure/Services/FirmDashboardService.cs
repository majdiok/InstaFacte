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
    private readonly ILogger<FirmDashboardService> _logger;

    public FirmDashboardService(
        MasterDbContext masterContext,
        ITenantService tenantService,
        ILogger<FirmDashboardService> logger)
    {
        _masterContext = masterContext;
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<FirmDashboardDto> GetDashboardAsync(Guid firmTenantId, CancellationToken cancellationToken = default)
    {
        var clients = await (
            from a in _masterContext.FirmClientAssignments.AsNoTracking()
            join t in _masterContext.Tenants.AsNoTracking() on a.CompanyTenantId equals t.Id
            where a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.Active
            orderby t.CompanyName
            select new { a, t.CompanyName }).ToListAsync(cancellationToken);

        var invitations = await _masterContext.FirmClientAssignments.AsNoTracking()
            .Where(a => a.FirmTenantId == firmTenantId && a.Status == FirmAssignmentStatus.PendingFirmApproval)
            .OrderByDescending(a => a.RequestedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var invitationRows = new List<FirmDashboardInvitationRowDto>();
        foreach (var inv in invitations)
        {
            var company = await _masterContext.Tenants.AsNoTracking()
                .FirstAsync(t => t.Id == inv.CompanyTenantId, cancellationToken);
            invitationRows.Add(new FirmDashboardInvitationRowDto
            {
                Id = inv.Id,
                CompanyName = company.CompanyName,
                RequestedAt = inv.RequestedAt,
                Notes = inv.Notes
            });
        }

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var clientRows = new List<FirmDashboardClientRowDto>();
        var inactiveCount = 0;
        var vatDraftsTotal = 0;

        foreach (var item in clients)
        {
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

        return new FirmDashboardDto
        {
            ActiveClientsCount = clientRows.Count,
            PendingInvitationsCount = invitationRows.Count,
            InactiveDossiersCount = inactiveCount,
            VatDraftsCount = vatDraftsTotal,
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
