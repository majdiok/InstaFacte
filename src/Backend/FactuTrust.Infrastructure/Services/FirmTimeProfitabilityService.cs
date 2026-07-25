using System.Globalization;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.FirmGovernance;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Coût = Hours × PrixHoraire ; Marge = BudgetAnnuel − Coût (Décisiel SuiviFeuilleDeTemps).
/// </summary>
public sealed class FirmTimeProfitabilityService : IFirmTimeProfitabilityService
{
    private readonly MasterDbContext _master;
    private readonly IFirmDossierAccessService _dossierAccess;
    private readonly ICurrentUser _currentUser;
    private readonly FirmGovernanceOptions _options;

    public FirmTimeProfitabilityService(
        MasterDbContext master,
        IFirmDossierAccessService dossierAccess,
        ICurrentUser currentUser,
        IOptions<FirmGovernanceOptions> options)
    {
        _master = master;
        _dossierAccess = dossierAccess;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public FirmHourlyRateSettingsDto GetHourlyRateSettingsAsync() =>
        new() { DefaultHourlyCostRate = _options.DefaultHourlyCostRate <= 0 ? 50m : _options.DefaultHourlyCostRate };

    Task<FirmHourlyRateSettingsDto> IFirmTimeProfitabilityService.GetHourlyRateSettingsAsync() =>
        Task.FromResult(GetHourlyRateSettingsAsync());

    public async Task<FirmDossierTimeProfitabilityReportDto> GetDossierTimeProfitabilityAsync(
        Guid firmTenantId,
        string? companySearch,
        int? year,
        Guid? collaboratorUserId,
        FirmMarginSignFilter marginFilter,
        CancellationToken cancellationToken = default)
    {
        var allowed = await ResolveAllowedAssignmentsAsync(firmTenantId, cancellationToken);
        if (allowed is { Count: 0 })
            return EmptyReport();

        var defaultRate = _options.DefaultHourlyCostRate <= 0 ? 50m : _options.DefaultHourlyCostRate;

        var tsQuery = _master.FirmTimeSheetEntries.AsNoTracking()
            .Where(t => t.FirmTenantId == firmTenantId && t.FirmClientAssignmentId != null);
        if (allowed is not null)
            tsQuery = tsQuery.Where(t => allowed.Contains(t.FirmClientAssignmentId!.Value));
        if (year.HasValue)
            tsQuery = tsQuery.Where(t => t.WorkDate.Year == year.Value);
        if (collaboratorUserId.HasValue)
            tsQuery = tsQuery.Where(t => t.UserId == collaboratorUserId.Value);

        var timeRows = await tsQuery.ToListAsync(cancellationToken);
        if (timeRows.Count == 0)
            return EmptyReport();

        var assignmentIds = timeRows.Select(t => t.FirmClientAssignmentId!.Value).Distinct().ToList();
        var years = timeRows.Select(t => t.WorkDate.Year).Distinct().ToList();

        var budgets = await _master.Set<FirmDossierYearBudget>().AsNoTracking()
            .Where(b => b.FirmTenantId == firmTenantId && assignmentIds.Contains(b.FirmClientAssignmentId) && years.Contains(b.Year))
            .ToListAsync(cancellationToken);

        var permanentFiles = await _master.PermanentFiles.AsNoTracking()
            .Where(p => p.FirmTenantId == firmTenantId && assignmentIds.Contains(p.FirmClientAssignmentId))
            .Select(p => new { p.FirmClientAssignmentId, p.AnnualFeeAmount, p.CompanyName })
            .ToListAsync(cancellationToken);

        var assignments = await _master.FirmClientAssignments.AsNoTracking()
            .Where(a => a.FirmTenantId == firmTenantId && assignmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.CompanyTenantId })
            .ToListAsync(cancellationToken);

        var companyIds = assignments.Select(a => a.CompanyTenantId).Distinct().ToList();
        var tenants = await _master.Tenants.AsNoTracking()
            .Where(t => companyIds.Contains(t.Id))
            .Select(t => new { t.Id, t.CompanyName })
            .ToListAsync(cancellationToken);

        var userIds = timeRows.Select(t => t.UserId).Distinct().ToList();
        var profiles = await _master.FirmCollaboratorProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.HourlyCostRate })
            .ToListAsync(cancellationToken);

        // Coûts employeur et paramètres d'exercice : chargés en une passe, le taux horaire étant
        // résolu par collaborateur ET par année.
        var costs = await _master.FirmCollaboratorYearCosts.AsNoTracking()
            .Where(c => c.FirmTenantId == firmTenantId
                        && userIds.Contains(c.CollaboratorUserId)
                        && years.Contains(c.Year))
            .ToListAsync(cancellationToken);

        var settingsByYear = await ResolveSettingsByYearAsync(firmTenantId, years, cancellationToken);

        var groups = timeRows.GroupBy(t => new
        {
            AssignmentId = t.FirmClientAssignmentId!.Value,
            Year = t.WorkDate.Year,
            t.UserId
        });

        var rows = new List<FirmDossierTimeProfitabilityRowDto>();
        foreach (var g in groups)
        {
            var pf = permanentFiles.FirstOrDefault(p => p.FirmClientAssignmentId == g.Key.AssignmentId);
            var assignment = assignments.FirstOrDefault(a => a.Id == g.Key.AssignmentId);
            var companyName = pf?.CompanyName
                ?? tenants.FirstOrDefault(t => t.Id == assignment?.CompanyTenantId)?.CompanyName
                ?? g.First().ClientCompanyName
                ?? "—";

            if (!string.IsNullOrWhiteSpace(companySearch)
                && companyName.IndexOf(companySearch.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var yearBudget = budgets.FirstOrDefault(b =>
                b.FirmClientAssignmentId == g.Key.AssignmentId && b.Year == g.Key.Year);
            var fromFallback = yearBudget is null;
            var budget = yearBudget?.BudgetAnnuel ?? pf?.AnnualFeeAmount ?? 0m;

            var resolution = HourlyCostRateCalculator.Resolve(
                costs.FirstOrDefault(c => c.CollaboratorUserId == g.Key.UserId && c.Year == g.Key.Year),
                settingsByYear[g.Key.Year],
                profiles.FirstOrDefault(p => p.UserId == g.Key.UserId)?.HourlyCostRate,
                defaultRate);

            var hours = g.Sum(t => t.Hours);
            var billableHours = g.Where(t => t.IsBillable).Sum(t => t.Hours);
            var nonBillableHours = hours - billableHours;

            // Le coût de revient intègre toutes les heures : une heure non facturable est passée
            // sur le dossier et payée. Le ratio ci-dessous expose la part improductive.
            var cost = MillimeRounding.Round(hours * resolution.Rate);
            var margin = MillimeRounding.Round(budget - cost);

            if (marginFilter == FirmMarginSignFilter.Negative && margin >= 0) continue;
            if (marginFilter == FirmMarginSignFilter.Positive && margin < 0) continue;

            rows.Add(new FirmDossierTimeProfitabilityRowDto
            {
                FirmClientAssignmentId = g.Key.AssignmentId,
                CompanyName = companyName,
                Year = g.Key.Year,
                CollaboratorUserId = g.Key.UserId,
                CollaboratorName = g.First().UserDisplayName,
                BudgetAnnuel = budget,
                TotalHours = hours,
                BillableHours = billableHours,
                NonBillableHours = nonBillableHours,
                BillableRatioPercent = hours <= 0 ? 0m : MillimeRounding.Round(billableHours / hours * 100m),
                HourlyRate = resolution.Rate,
                HourlyRateSource = (int)resolution.Source,
                HourlyRateSourceDisplay = DescribeRateSource(resolution.Source),
                HourlyRateBasis = resolution.Basis,
                Cost = cost,
                Margin = margin,
                BudgetFromFallback = fromFallback
            });
        }

        rows = rows.OrderBy(r => r.CompanyName).ThenBy(r => r.Year).ThenBy(r => r.CollaboratorName).ToList();

        var uniqueBudgetSum = rows
            .GroupBy(r => new { r.FirmClientAssignmentId, r.Year })
            .Sum(g => g.First().BudgetAnnuel);

        return new FirmDossierTimeProfitabilityReportDto
        {
            Rows = rows,
            TotalHours = rows.Sum(r => r.TotalHours),
            TotalBillableHours = rows.Sum(r => r.BillableHours),
            TotalNonBillableHours = rows.Sum(r => r.NonBillableHours),
            UniqueBudgetSum = uniqueBudgetSum,
            HoursByYear = rows
                .GroupBy(r => r.Year)
                .OrderBy(g => g.Key)
                .Select(g => new FirmChartSliceDto { Label = g.Key.ToString(), Value = g.Sum(x => x.TotalHours) })
                .ToList(),
            HoursByCompany = rows
                .GroupBy(r => r.CompanyName)
                .OrderByDescending(g => g.Sum(x => x.TotalHours))
                .Take(12)
                .Select(g => new FirmChartSliceDto { Label = g.Key, Value = g.Sum(x => x.TotalHours) })
                .ToList()
        };
    }

    public async Task<Result<FirmDossierYearBudgetDto>> UpsertYearBudgetAsync(
        Guid firmTenantId,
        Guid assignmentId,
        int year,
        decimal budgetAnnuel,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _master.FirmClientAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.FirmTenantId == firmTenantId, cancellationToken);
        if (assignment is null)
            return Result.Failure<FirmDossierYearBudgetDto>(Error.NotFound("Assignment", assignmentId));

        var existing = await _master.Set<FirmDossierYearBudget>()
            .FirstOrDefaultAsync(b =>
                b.FirmTenantId == firmTenantId
                && b.FirmClientAssignmentId == assignmentId
                && b.Year == year, cancellationToken);

        if (existing is null)
        {
            var create = FirmDossierYearBudget.Create(firmTenantId, assignmentId, year, budgetAnnuel);
            if (create.IsFailure)
                return Result.Failure<FirmDossierYearBudgetDto>(create.Error);
            _master.Set<FirmDossierYearBudget>().Add(create.Value);
            await _master.SaveChangesAsync(cancellationToken);
            return Result.Success(new FirmDossierYearBudgetDto
            {
                FirmClientAssignmentId = assignmentId,
                Year = year,
                BudgetAnnuel = create.Value.BudgetAnnuel,
                FromFallback = false
            });
        }

        var set = existing.SetBudget(budgetAnnuel);
        if (set.IsFailure)
            return Result.Failure<FirmDossierYearBudgetDto>(set.Error);
        await _master.SaveChangesAsync(cancellationToken);
        return Result.Success(new FirmDossierYearBudgetDto
        {
            FirmClientAssignmentId = assignmentId,
            Year = year,
            BudgetAnnuel = existing.BudgetAnnuel,
            FromFallback = false
        });
    }

    public async Task<byte[]> ExportDossierTimeProfitabilityPdfAsync(
        Guid firmTenantId,
        string? companySearch,
        int? year,
        Guid? collaboratorUserId,
        FirmMarginSignFilter marginFilter,
        CancellationToken cancellationToken = default)
    {
        var report = await GetDossierTimeProfitabilityAsync(
            firmTenantId, companySearch, year, collaboratorUserId, marginFilter, cancellationToken);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.Header().Text("Feuilles de temps et rentabilité par dossier").FontSize(14).Bold();
                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);      // Société
                        c.ConstantColumn(40);     // Année
                        c.RelativeColumn(1.5f);   // Collaborateur
                        c.ConstantColumn(65);     // Budget
                        c.ConstantColumn(50);     // Heures
                        c.ConstantColumn(50);     // Dont facturables
                        c.ConstantColumn(50);     // Taux
                        c.ConstantColumn(55);     // Origine du taux
                        c.ConstantColumn(65);     // Coût
                        c.ConstantColumn(65);     // Marge
                    });
                    table.Header(h =>
                    {
                        foreach (var label in new[]
                                 {
                                     "Société", "Année", "Collaborateur", "Budget", "Heures", "Dont fact.",
                                     "Taux", "Origine", "Coût", "Marge"
                                 })
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(label).Bold().FontSize(8);
                    });
                    foreach (var r in report.Rows)
                    {
                        table.Cell().Padding(2).Text(r.CompanyName).FontSize(8);
                        table.Cell().Padding(2).Text(r.Year.ToString()).FontSize(8);
                        table.Cell().Padding(2).Text(r.CollaboratorName).FontSize(8);
                        table.Cell().Padding(2).AlignRight().Text(Fmt(r.BudgetAnnuel)).FontSize(8);
                        table.Cell().Padding(2).AlignRight().Text(Fmt(r.TotalHours)).FontSize(8);
                        table.Cell().Padding(2).AlignRight().Text(Fmt(r.BillableHours)).FontSize(8);
                        table.Cell().Padding(2).AlignRight().Text(Fmt(r.HourlyRate)).FontSize(8);
                        table.Cell().Padding(2).Text(r.HourlyRateSourceDisplay).FontSize(7);
                        table.Cell().Padding(2).AlignRight().Text(Fmt(r.Cost)).FontSize(8);
                        table.Cell().Padding(2).AlignRight().Text(Fmt(r.Margin)).FontSize(8);
                    }
                });
                page.Footer().AlignRight().Text(
                        $"Total heures : {Fmt(report.TotalHours)} — dont facturables {Fmt(report.TotalBillableHours)}")
                    .FontSize(9);
            });
        }).GeneratePdf();
    }

    private async Task<IReadOnlySet<Guid>?> ResolveAllowedAssignmentsAsync(Guid firmTenantId, CancellationToken ct)
    {
        if (_currentUser.TryGetAccessScope(out var scope))
            return await _dossierAccess.GetAccessibleAssignmentIdsAsync(firmTenantId, scope, ct);
        if (_currentUser.IsAuthenticated && _currentUser.Role is UserRole.FirmAccountant)
            return new HashSet<Guid>();
        return null;
    }

    /// <summary>
    /// Paramètres d'exercice pour chaque année présente dans le rapport.
    /// </summary>
    /// <remarks>
    /// Les exercices non paramétrés reçoivent les défauts tunisiens sans écriture en base : une
    /// consultation de rapport ne doit pas créer de lignes de configuration.
    /// </remarks>
    private async Task<Dictionary<int, FirmTimeSheetYearSettings>> ResolveSettingsByYearAsync(
        Guid firmTenantId, IReadOnlyList<int> years, CancellationToken cancellationToken)
    {
        var persisted = await _master.FirmTimeSheetYearSettings.AsNoTracking()
            .Where(s => s.FirmTenantId == firmTenantId && years.Contains(s.Year))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, FirmTimeSheetYearSettings>();
        foreach (var year in years)
        {
            var match = persisted.FirstOrDefault(s => s.Year == year);
            result[year] = match ?? FirmTimeSheetYearSettings.Create(firmTenantId, year).Value;
        }
        return result;
    }

    private static string DescribeRateSource(FirmHourlyRateSource source) => source switch
    {
        FirmHourlyRateSource.Derived => "Calculé",
        FirmHourlyRateSource.Override => "Imposé",
        FirmHourlyRateSource.LegacyProfile => "Profil",
        _ => "Défaut cabinet"
    };

    private static FirmDossierTimeProfitabilityReportDto EmptyReport() => new();

    private static string Fmt(decimal v) => v.ToString("#,##0.00", CultureInfo.GetCultureInfo("fr-FR"));
}
