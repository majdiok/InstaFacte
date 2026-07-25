using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmFiscalOpsAggregator : IFirmFiscalOpsAggregator
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<FirmFiscalOpsAggregator> _logger;

    public FirmFiscalOpsAggregator(
        ITenantService tenantService,
        ILogger<FirmFiscalOpsAggregator> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<FirmFiscalOpsSummaryDto> AggregateAsync(
        IReadOnlyList<Guid> companyTenantIds,
        CancellationToken cancellationToken = default)
    {
        var overdue = 0;
        var upcoming7 = 0;
        var tejPending = 0;
        var liasseDrafts = 0;
        var dtsPending = 0;
        var overdueAmount = 0m;
        var upcoming7Amount = 0m;
        var today = DateTime.UtcNow.Date;

        foreach (var tenantId in companyTenantIds)
        {
            try
            {
                var conn = await _tenantService.GetConnectionStringAsync(tenantId, cancellationToken);
                if (string.IsNullOrEmpty(conn))
                    continue;

                await using var ctx = CreateTenantContext(conn);
                var schedules = await ctx.FiscalScheduleEntries.AsNoTracking()
                    .Where(e => !e.IsCancelled)
                    .ToListAsync(cancellationToken);

                foreach (var entry in schedules)
                {
                    var status = entry.ResolveStatus(today);
                    switch (status)
                    {
                        case FiscalScheduleStatus.Overdue:
                            overdue++;
                            overdueAmount += entry.EstimatedAmount;
                            if (entry.ObligationType == FiscalObligationType.WithholdingTax)
                                tejPending++;
                            if (entry.ObligationType == FiscalObligationType.CnssDtsQuarterly)
                                dtsPending++;
                            break;
                        case FiscalScheduleStatus.UpcomingWithin7Days:
                            upcoming7++;
                            upcoming7Amount += entry.EstimatedAmount;
                            if (entry.ObligationType == FiscalObligationType.WithholdingTax && entry.DepositDate is null)
                                tejPending++;
                            if (entry.ObligationType == FiscalObligationType.CnssDtsQuarterly && entry.DepositDate is null)
                                dtsPending++;
                            break;
                    }
                }

                var currentYear = today.Year;
                liasseDrafts += await ctx.Set<FiscalResultDeclaration>().AsNoTracking()
                    .CountAsync(
                        d => d.Status == FiscalDeclarationStatus.Draft && d.FiscalYear >= currentYear - 1,
                        cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed fiscal ops aggregation for tenant {TenantId}", tenantId);
            }
        }

        return new FirmFiscalOpsSummaryDto
        {
            OverdueSchedulesCount = overdue,
            UpcomingWithin7DaysCount = upcoming7,
            TejPendingCount = tejPending,
            LiasseDraftsCount = liasseDrafts,
            DtsPendingCount = dtsPending,
            OverdueEstimatedAmount = overdueAmount,
            Upcoming7DaysEstimatedAmount = upcoming7Amount
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
