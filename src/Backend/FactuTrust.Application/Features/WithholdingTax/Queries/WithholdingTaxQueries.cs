using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.WithholdingTax.Queries;

// --- Get Types ---
public record GetWithholdingTaxTypesQuery(bool ActiveOnly = true) : IRequest<List<WithholdingTaxTypeDto>>;

public class GetWithholdingTaxTypesHandler : IRequestHandler<GetWithholdingTaxTypesQuery, List<WithholdingTaxTypeDto>>
{
    private readonly IWithholdingTaxRepository _repo;

    public GetWithholdingTaxTypesHandler(IWithholdingTaxRepository repo) => _repo = repo;

    public async Task<List<WithholdingTaxTypeDto>> Handle(GetWithholdingTaxTypesQuery query, CancellationToken ct)
    {
        var types = query.ActiveOnly
            ? await _repo.GetActiveTypesAsync(ct)
            : await _repo.GetAllTypesAsync(ct);

        return types.Select(t => new WithholdingTaxTypeDto(
            t.Id, t.Code, t.Category, t.Category.ToDisplayString(),
            t.Label, t.LabelAr, t.DefaultRate, t.ArticleReference,
            t.ApplicableToResident, t.ApplicableToNonResident,
            t.MinimumThreshold, t.IsActive, t.IsSystem, t.DisplayOrder)).ToList();
    }
}

// --- Dashboard (payment-date year scope) ---
public record GetWithholdingDashboardQuery(int? Year = null) : IRequest<WithholdingDashboardDto>;

public class GetWithholdingDashboardHandler : IRequestHandler<GetWithholdingDashboardQuery, WithholdingDashboardDto>
{
    private readonly ISupplierInvoiceRepository _invoices;
    private readonly IWithholdingComplianceService _compliance;
    private readonly IPaymentRepository _payments;

    public GetWithholdingDashboardHandler(
        ISupplierInvoiceRepository invoices,
        IWithholdingComplianceService compliance,
        IPaymentRepository payments)
    {
        _invoices = invoices;
        _compliance = compliance;
        _payments = payments;
    }

    public async Task<WithholdingDashboardDto> Handle(GetWithholdingDashboardQuery query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dashboardYear = query.Year ?? now.Year;

        var aggregates = await _invoices.GetMonthlyWithholdingAggregatesForPaidInvoicesAsync(dashboardYear, ct);
        var byMonth = aggregates.ToDictionary(a => a.Month);
        var breakdown = Enumerable.Range(1, 12).Select(m =>
        {
            if (byMonth.TryGetValue(m, out var row))
                return new WithholdingDashboardMonthBreakdownDto(m, row.Count, row.TotalHT, row.TotalWithheld, row.TotalNetPaid);
            return new WithholdingDashboardMonthBreakdownDto(m, 0, 0, 0, 0);
        }).ToList();

        const int drafts = 0;
        const int validated = 0;
        const int submitted = 0;

        var totalWithheldForYear = breakdown.Sum(b => b.TotalWithheld);
        var totalCertificatesForYear = breakdown.Sum(b => b.CertificateCount);

        var nextDeadline = _compliance.GetNextTejDeadline(now.Year, now.Month);

        var alerts = new List<string>();
        if ((nextDeadline - now).TotalDays <= 5)
            alerts.Add($"Échéance TEJ proche : {nextDeadline:dd/MM/yyyy}");

        var subieRows = await _payments.GetClientWithholdingAggregatesByYearAsync(dashboardYear, ct);
        var subieByMonthMap = subieRows.ToDictionary(r => r.Month);
        var subieBreakdown = Enumerable.Range(1, 12).Select(m =>
        {
            if (subieByMonthMap.TryGetValue(m, out var row))
                return new ClientWithholdingSubieMonthDto(m, row.TotalSubie, row.PaymentCount);
            return new ClientWithholdingSubieMonthDto(m, 0, 0);
        }).ToList();
        var totalSubieYear = subieBreakdown.Sum(x => x.TotalSubie);

        return new WithholdingDashboardDto(
            dashboardYear,
            totalWithheldForYear,
            totalCertificatesForYear,
            drafts,
            validated,
            submitted,
            nextDeadline,
            breakdown,
            alerts,
            totalSubieYear,
            subieBreakdown);
    }
}

// --- Monthly Report ---
public record GetWithholdingMonthlyReportQuery(int Year, int Month) : IRequest<WithholdingMonthlyReportDto>;

public class GetWithholdingMonthlyReportHandler : IRequestHandler<GetWithholdingMonthlyReportQuery, WithholdingMonthlyReportDto>
{
    private readonly ISupplierInvoiceRepository _invoices;
    private readonly IWithholdingTaxRepository _whRepo;

    public GetWithholdingMonthlyReportHandler(ISupplierInvoiceRepository invoices, IWithholdingTaxRepository whRepo)
    {
        _invoices = invoices;
        _whRepo = whRepo;
    }

    public async Task<WithholdingMonthlyReportDto> Handle(GetWithholdingMonthlyReportQuery query, CancellationToken ct)
    {
        var invoices = await _invoices.GetPaidWithholdingInvoicesForTejPeriodAsync(query.Year, query.Month, ct);

        var rows = new List<(string Prefix, decimal Ht, decimal Wh, Guid InvoiceId)>();
        foreach (var inv in invoices)
        {
            var prefix = "RS7";
            if (inv.WithholdingTaxTypeId.HasValue)
            {
                var wt = await _whRepo.GetTypeByIdAsync(inv.WithholdingTaxTypeId.Value, ct);
                if (wt is not null && !string.IsNullOrWhiteSpace(wt.Code))
                    prefix = wt.Code.Split('_')[0];
            }

            var wh = inv.WithholdingAmount ?? 0;
            rows.Add((prefix, inv.SubTotal.Amount, wh, inv.Id));
        }

        var byCategory = rows
            .GroupBy(x => x.Prefix)
            .Select(g =>
            {
                var category = g.Key switch
                {
                    "RS1" => WithholdingCategory.Loyers,
                    "RS2" => WithholdingCategory.Honoraires,
                    "RS3" => WithholdingCategory.RevenusCapitaux,
                    "RS4" => WithholdingCategory.Dividendes,
                    "RS5" => WithholdingCategory.PlusValues,
                    "RS6" => WithholdingCategory.ImmobilierFoncier,
                    "RS7" => WithholdingCategory.Achats,
                    "RS8" => WithholdingCategory.Jeux,
                    "RS9" => WithholdingCategory.NonResidents,
                    _ => WithholdingCategory.Commissions
                };

                return new WithholdingReportByCategoryDto(
                    category,
                    category.ToDisplayString(),
                    g.Sum(x => x.Ht),
                    g.Sum(x => x.Wh),
                    g.Select(x => x.InvoiceId).Distinct().Count());
            }).ToList();

        return new WithholdingMonthlyReportDto(
            query.Year,
            query.Month,
            byCategory,
            invoices.Sum(i => i.SubTotal.Amount),
            invoices.Sum(i => i.WithholdingAmount ?? 0),
            invoices.Count);
    }
}

// --- TEJ export history ---
public record GetTejXmlExportLogsQuery(int Take = 50) : IRequest<List<TejXmlExportLogListItemDto>>;

public class GetTejXmlExportLogsHandler : IRequestHandler<GetTejXmlExportLogsQuery, List<TejXmlExportLogListItemDto>>
{
    private readonly ITejXmlExportLogRepository _logs;

    public GetTejXmlExportLogsHandler(ITejXmlExportLogRepository logs) => _logs = logs;

    public async Task<List<TejXmlExportLogListItemDto>> Handle(GetTejXmlExportLogsQuery query, CancellationToken ct)
    {
        var rows = await _logs.GetRecentAsync(query.Take, ct);
        return rows.Select(e => new TejXmlExportLogListItemDto(
            e.Id,
            e.CreatedAt,
            e.FileName,
            e.Year,
            e.Month,
            e.SubmissionType,
            e.Sha256Hex,
            e.CertificateCount,
            e.IsValid,
            e.ExportedByEmail)).ToList();
    }
}

// --- TEJ eligible supplier invoices (paid + RS, PaidAt in month) ---
public record GetTejEligibleInvoiceCountQuery(int Year, int Month) : IRequest<int>;

public sealed class GetTejEligibleInvoiceCountHandler : IRequestHandler<GetTejEligibleInvoiceCountQuery, int>
{
    private readonly ISupplierInvoiceRepository _invoices;

    public GetTejEligibleInvoiceCountHandler(ISupplierInvoiceRepository invoices) => _invoices = invoices;

    public Task<int> Handle(GetTejEligibleInvoiceCountQuery query, CancellationToken ct) =>
        _invoices.CountPaidWithholdingInvoicesForTejPeriodAsync(query.Year, query.Month, ct);
}
