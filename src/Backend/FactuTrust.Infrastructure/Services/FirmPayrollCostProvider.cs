using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Agrège le coût employeur des salariés du cabinet depuis les bulletins de son propre tenant.
/// </summary>
/// <remarks>
/// Coût employeur = brut + CNSS patronale + accident du travail + TFP + FOPROLOS, tel que déjà
/// calculé et figé sur chaque bulletin par le module Paie. On ne recalcule rien ici : reprendre
/// les taux à notre compte ferait diverger la rentabilité de la paie réellement déclarée.
/// </remarks>
public sealed class FirmPayrollCostProvider : IFirmPayrollCostProvider
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<FirmPayrollCostProvider> _logger;

    public FirmPayrollCostProvider(ITenantService tenantService, ILogger<FirmPayrollCostProvider> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<FirmPayrollCostSnapshotDto> GetAnnualEmployerCostsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = await _tenantService.GetConnectionStringAsync(firmTenantId, cancellationToken);
            if (string.IsNullOrEmpty(connectionString))
                return FirmPayrollCostSnapshotDto.Unavailable(
                    "Aucune base de paie n'est rattachée au cabinet.");

            await using var context = CreateTenantContext(connectionString);

            // Seules les paies arrêtées comptent : un brouillon ou un calcul non validé ne doit pas
            // peser sur une rentabilité présentée en revue.
            var closedRunIds = await context.PayrollRuns.AsNoTracking()
                .Where(r => r.Year == year
                            && (r.Status == PayrollRunStatus.Validated || r.Status == PayrollRunStatus.Closed))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            if (closedRunIds.Count == 0)
                return FirmPayrollCostSnapshotDto.Unavailable(
                    $"Aucune paie validée ou clôturée pour l'exercice {year}.");

            var payslips = await context.Payslips.AsNoTracking()
                .Where(p => closedRunIds.Contains(p.PayrollRunId))
                .Select(p => new
                {
                    p.EmployeeId,
                    p.EmployeeName,
                    p.GrossSalary,
                    p.CnssEmployer,
                    p.WorkAccidentContribution,
                    p.Tfp,
                    p.Foprolos
                })
                .ToListAsync(cancellationToken);

            if (payslips.Count == 0)
                return FirmPayrollCostSnapshotDto.Unavailable(
                    $"Aucun bulletin sur les paies arrêtées de l'exercice {year}.");

            var employees = payslips
                .GroupBy(p => p.EmployeeId)
                .Select(g => new FirmPayrollEmployeeCostDto(
                    g.Key,
                    g.First().EmployeeName,
                    MillimeRounding.Round(g.Sum(p => p.GrossSalary)),
                    MillimeRounding.Round(g.Sum(p =>
                        p.CnssEmployer + p.WorkAccidentContribution + p.Tfp + p.Foprolos)),
                    g.Count()))
                .OrderBy(e => e.EmployeeName)
                .ToList();

            return FirmPayrollCostSnapshotDto.Available(employees);
        }
        catch (Exception ex)
        {
            // La rentabilité doit rester consultable base de paie éteinte : on journalise et on
            // laisse le cabinet retomber sur la saisie manuelle.
            _logger.LogWarning(
                ex, "Import du coût de paie impossible pour le cabinet {TenantId}, exercice {Year}", firmTenantId, year);
            return FirmPayrollCostSnapshotDto.Unavailable(
                "La paie du cabinet n'a pas pu être interrogée. Saisissez le coût employeur manuellement.");
        }
    }

    private static TenantDbContext CreateTenantContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new TenantDbContext(options);
    }
}
