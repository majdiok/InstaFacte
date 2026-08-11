using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Agrège le coût employeur des salariés du cabinet depuis les bulletins de son propre tenant.
/// </summary>
/// <remarks>
/// Coût employeur = brut + CNSS patronale + accident du travail + TFP + FOPROLOS + CSS patronale, tel que déjà
/// calculé et figé sur chaque bulletin par le module Paie. On ne recalcule rien ici : reprendre
/// les taux à notre compte ferait diverger la rentabilité de la paie réellement déclarée.
/// </remarks>
public sealed class FirmPayrollCostProvider : IFirmPayrollCostProvider
{
    private readonly FirmTenantPayrollAccessor _payrollAccess;
    private readonly ILogger<FirmPayrollCostProvider> _logger;

    public FirmPayrollCostProvider(
        FirmTenantPayrollAccessor payrollAccess,
        ILogger<FirmPayrollCostProvider> logger)
    {
        _payrollAccess = payrollAccess;
        _logger = logger;
    }

    public async Task<FirmPayrollCostSnapshotDto> GetAnnualEmployerCostsAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _payrollAccess.OpenAsync(firmTenantId, cancellationToken);
            if (context is null)
                return FirmPayrollCostSnapshotDto.Unavailable(
                    "Aucune base de paie n'est rattachée au cabinet.");

            var activeEmployees = await context.Set<Domain.Entities.Payroll.Employee>().AsNoTracking()
                .CountAsync(e => e.IsActive, cancellationToken);
            if (activeEmployees == 0)
                return FirmPayrollCostSnapshotDto.Unavailable(
                    "Aucun salarié actif dans la paie interne du cabinet. Provisionnez les collaborateurs depuis la page Coûts collaborateurs.");

            // Seules les paies arrêtées comptent : un brouillon ou un calcul non validé ne doit pas
            // peser sur une rentabilité présentée en revue.
            var closedRunIds = await context.PayrollRuns.AsNoTracking()
                .Where(r => r.Year == year
                            && (r.Status == PayrollRunStatus.Validated || r.Status == PayrollRunStatus.Closed))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            if (closedRunIds.Count == 0)
                return FirmPayrollCostSnapshotDto.Unavailable(
                    $"Aucune paie validée ou clôturée pour l'exercice {year}.",
                    activeEmployees);

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
                    p.Foprolos,
                    p.CssEmployer
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
                        p.CnssEmployer + p.WorkAccidentContribution + p.Tfp + p.Foprolos + p.CssEmployer)),
                    g.Count()))
                .OrderBy(e => e.EmployeeName)
                .ToList();

            return FirmPayrollCostSnapshotDto.Available(employees, activeEmployees);
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

    public async Task<DateTime?> GetLatestPayrollActivityAtAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _payrollAccess.OpenAsync(firmTenantId, cancellationToken);
            if (context is null)
                return null;

            var runs = await context.PayrollRuns.AsNoTracking()
                .Where(r => r.Year == year
                            && (r.Status == PayrollRunStatus.Validated || r.Status == PayrollRunStatus.Closed))
                .Select(r => new { r.ValidatedAt, r.ClosedAt })
                .ToListAsync(cancellationToken);

            if (runs.Count == 0)
                return null;

            return runs
                .Select(r => r.ClosedAt ?? r.ValidatedAt)
                .Where(d => d.HasValue)
                .Max();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Impossible de lire la dernière activité paie pour le cabinet {TenantId}, exercice {Year}",
                firmTenantId,
                year);
            return null;
        }
    }

    public async Task<IReadOnlyList<FirmPayrollEmployeeLinkDto>> GetActiveEmployeesForLinkingAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _payrollAccess.OpenAsync(firmTenantId, cancellationToken);
            if (context is null)
                return Array.Empty<FirmPayrollEmployeeLinkDto>();

            var rows = await context.Set<Domain.Entities.Payroll.Employee>().AsNoTracking()
                .Where(e => e.IsActive)
                .Select(e => new
                {
                    e.Id,
                    Email = e.Email != null ? e.Email.Value : null,
                    e.FirstName,
                    e.LastName
                })
                .ToListAsync(cancellationToken);

            return rows
                .Select(e => new FirmPayrollEmployeeLinkDto(
                    e.Id,
                    e.Email,
                    $"{e.FirstName} {e.LastName}".Trim()))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Impossible de lister les salariés paie pour le cabinet {TenantId}",
                firmTenantId);
            return Array.Empty<FirmPayrollEmployeeLinkDto>();
        }
    }
}
