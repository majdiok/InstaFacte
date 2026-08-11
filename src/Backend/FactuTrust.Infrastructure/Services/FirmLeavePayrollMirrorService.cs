using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <inheritdoc cref="IFirmLeavePayrollMirrorService"/>
public sealed class FirmLeavePayrollMirrorService : IFirmLeavePayrollMirrorService
{
    private readonly MasterDbContext _master;
    private readonly FirmTenantPayrollAccessor _payrollAccess;
    private readonly ILogger<FirmLeavePayrollMirrorService> _logger;

    public FirmLeavePayrollMirrorService(
        MasterDbContext master,
        FirmTenantPayrollAccessor payrollAccess,
        ILogger<FirmLeavePayrollMirrorService> logger)
    {
        _master = master;
        _payrollAccess = payrollAccess;
        _logger = logger;
    }

    public async Task<FirmLeaveMirrorResultDto> MirrorApprovedAsync(
        Guid firmTenantId,
        Guid firmLeaveRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _master.FirmLeaveRequests
            .FirstOrDefaultAsync(
                r => r.Id == firmLeaveRequestId && r.FirmTenantId == firmTenantId, cancellationToken);
        if (request is null)
            return Describe(FirmLeavePayrollMirrorState.Failed, null, "Demande de congé introuvable.");

        if (!request.ShouldMirrorToPayroll())
            return await RevokeCoreAsync(firmTenantId, request, cancellationToken);

        var leaveType = await _master.FirmLeaveTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.LeaveTypeId, cancellationToken);

        if (leaveType?.PayrollLeaveType is not LeaveType payrollType)
        {
            // Télétravail, formation… : absence de mapping voulue, pas une anomalie.
            request.MarkPayrollMirror(
                FirmLeavePayrollMirrorState.NoPayrollEffect,
                null,
                "Ce type d'absence n'a pas d'effet sur le bulletin.");
            await _master.SaveChangesAsync(cancellationToken);
            return Describe(FirmLeavePayrollMirrorState.NoPayrollEffect, null, request.PayrollMirrorMessage);
        }

        var payrollEmployeeId = await ResolvePayrollEmployeeIdAsync(request.UserId, cancellationToken);
        if (payrollEmployeeId is null)
        {
            return await PersistOutcomeAsync(
                request,
                FirmLeavePayrollMirrorState.Failed,
                null,
                "Le collaborateur n'est lié à aucun salarié de la paie interne.",
                cancellationToken);
        }

        try
        {
            await using var tenant = await _payrollAccess.OpenAsync(firmTenantId, cancellationToken);
            if (tenant is null)
            {
                return await PersistOutcomeAsync(
                    request,
                    FirmLeavePayrollMirrorState.Failed,
                    request.PayrollLeaveRequestId,
                    "Aucune base de paie n'est rattachée au cabinet.",
                    cancellationToken);
            }

            // Les bulletins arrêtés sont gelés : rien ne doit être écrit sur un mois déjà validé
            // ou clôturé, sous peine de faire diverger le bulletin émis de sa base de calcul.
            var frozenMonth = await FindFrozenMonthAsync(tenant, request.StartDate, request.EndDate, cancellationToken);
            if (frozenMonth is not null)
            {
                return await PersistOutcomeAsync(
                    request,
                    FirmLeavePayrollMirrorState.BlockedFrozenPayroll,
                    request.PayrollLeaveRequestId,
                    $"La paie de {frozenMonth} est arrêtée : ce congé n'aura pas d'effet sur le bulletin déjà émis. Traitez-le en régularisation.",
                    cancellationToken);
            }

            var mirror = request.PayrollLeaveRequestId is Guid existingId
                ? await tenant.LeaveRequests.FirstOrDefaultAsync(l => l.Id == existingId, cancellationToken)
                : null;

            if (mirror is null)
            {
                var created = LeaveRequest.Create(
                    payrollEmployeeId.Value,
                    payrollType,
                    request.StartDate,
                    request.EndDate,
                    request.Days,
                    BuildReason(request.Reason));
                if (created.IsFailure)
                {
                    return await PersistOutcomeAsync(
                        request,
                        FirmLeavePayrollMirrorState.Failed,
                        null,
                        created.Error.Description,
                        cancellationToken);
                }

                mirror = created.Value;
                tenant.LeaveRequests.Add(mirror);
            }
            else
            {
                // Ré-approbation après modification : on réaligne au lieu de créer un doublon.
                var updated = mirror.Update(
                    payrollType,
                    request.StartDate,
                    request.EndDate,
                    request.Days,
                    BuildReason(request.Reason));
                if (updated.IsFailure)
                {
                    return await PersistOutcomeAsync(
                        request,
                        FirmLeavePayrollMirrorState.Failed,
                        mirror.Id,
                        updated.Error.Description,
                        cancellationToken);
                }
            }

            mirror.Approve(request.ProcessedByName ?? "Congés cabinet");
            await tenant.SaveChangesAsync(cancellationToken);

            return await PersistOutcomeAsync(
                request,
                FirmLeavePayrollMirrorState.Mirrored,
                mirror.Id,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-open : l'approbation RH reste acquise, l'écart est rattrapable au rapprochement.
            _logger.LogWarning(
                ex,
                "Report du congé {LeaveId} vers la paie du cabinet {TenantId} impossible.",
                firmLeaveRequestId,
                firmTenantId);
            return await PersistOutcomeAsync(
                request,
                FirmLeavePayrollMirrorState.Failed,
                request.PayrollLeaveRequestId,
                "La paie du cabinet n'a pas pu être mise à jour. Relancez le report depuis le rapprochement.",
                cancellationToken);
        }
    }

    public async Task<FirmLeaveMirrorResultDto> RevokeAsync(
        Guid firmTenantId,
        Guid firmLeaveRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _master.FirmLeaveRequests
            .FirstOrDefaultAsync(
                r => r.Id == firmLeaveRequestId && r.FirmTenantId == firmTenantId, cancellationToken);
        return request is null
            ? Describe(FirmLeavePayrollMirrorState.Failed, null, "Demande de congé introuvable.")
            : await RevokeCoreAsync(firmTenantId, request, cancellationToken);
    }

    private async Task<FirmLeaveMirrorResultDto> RevokeCoreAsync(
        Guid firmTenantId,
        Domain.Entities.FirmGovernance.FirmLeaveRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PayrollLeaveRequestId is not Guid mirrorId)
        {
            return await PersistOutcomeAsync(
                request, FirmLeavePayrollMirrorState.NotMirrored, null, null, cancellationToken);
        }

        try
        {
            await using var tenant = await _payrollAccess.OpenAsync(firmTenantId, cancellationToken);
            if (tenant is null)
            {
                return await PersistOutcomeAsync(
                    request,
                    FirmLeavePayrollMirrorState.Failed,
                    mirrorId,
                    "Aucune base de paie n'est rattachée au cabinet.",
                    cancellationToken);
            }

            var frozenMonth = await FindFrozenMonthAsync(tenant, request.StartDate, request.EndDate, cancellationToken);
            if (frozenMonth is not null)
            {
                return await PersistOutcomeAsync(
                    request,
                    FirmLeavePayrollMirrorState.BlockedFrozenPayroll,
                    mirrorId,
                    $"La paie de {frozenMonth} est arrêtée : le congé a déjà été pris en compte sur le bulletin émis.",
                    cancellationToken);
            }

            var mirror = await tenant.LeaveRequests
                .FirstOrDefaultAsync(l => l.Id == mirrorId, cancellationToken);
            if (mirror is not null)
            {
                tenant.LeaveRequests.Remove(mirror);
                await tenant.SaveChangesAsync(cancellationToken);
            }

            return await PersistOutcomeAsync(
                request, FirmLeavePayrollMirrorState.Revoked, null, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Retrait du congé de paie {MirrorId} impossible pour le cabinet {TenantId}.",
                mirrorId,
                firmTenantId);
            return await PersistOutcomeAsync(
                request,
                FirmLeavePayrollMirrorState.Failed,
                mirrorId,
                "Le congé de paie n'a pas pu être retiré. Relancez depuis le rapprochement.",
                cancellationToken);
        }
    }

    /// <summary>Salarié de paie rattaché au collaborateur, via l'unique pont existant.</summary>
    private async Task<Guid?> ResolvePayrollEmployeeIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await _master.FirmCollaboratorProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.PayrollEmployeeId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Premier mois de la période dont la paie est arrêtée, ou <c>null</c>.</summary>
    private static async Task<string?> FindFrozenMonthAsync(
        TenantDbContext tenant,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var months = EnumerateMonths(startDate, endDate).ToList();
        var years = months.Select(m => m.Year).Distinct().ToList();

        var frozen = await tenant.PayrollRuns.AsNoTracking()
            .Where(r => years.Contains(r.Year)
                        && (r.Status == PayrollRunStatus.Validated || r.Status == PayrollRunStatus.Closed))
            .Select(r => new { r.Year, r.Month })
            .ToListAsync(cancellationToken);
        if (frozen.Count == 0)
            return null;

        var frozenSet = frozen.Select(f => (f.Year, f.Month)).ToHashSet();
        foreach (var month in months)
        {
            if (frozenSet.Contains((month.Year, month.Month)))
                return $"{month.Month:00}/{month.Year}";
        }

        return null;
    }

    private static IEnumerable<(int Year, int Month)> EnumerateMonths(DateTime startDate, DateTime endDate)
    {
        var cursor = new DateTime(startDate.Year, startDate.Month, 1);
        var last = new DateTime(endDate.Year, endDate.Month, 1);
        while (cursor <= last)
        {
            yield return (cursor.Year, cursor.Month);
            cursor = cursor.AddMonths(1);
        }
    }

    private static string BuildReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? "Congé cabinet"
            : $"Congé cabinet — {reason.Trim()}";

    private async Task<FirmLeaveMirrorResultDto> PersistOutcomeAsync(
        Domain.Entities.FirmGovernance.FirmLeaveRequest request,
        FirmLeavePayrollMirrorState state,
        Guid? payrollLeaveRequestId,
        string? message,
        CancellationToken cancellationToken)
    {
        request.MarkPayrollMirror(state, payrollLeaveRequestId, message);
        await _master.SaveChangesAsync(cancellationToken);
        return Describe(state, payrollLeaveRequestId, message);
    }

    private static FirmLeaveMirrorResultDto Describe(
        FirmLeavePayrollMirrorState state,
        Guid? payrollLeaveRequestId,
        string? message) =>
        new()
        {
            State = (int)state,
            StateDisplay = DescribeState(state),
            Message = message,
            PayrollLeaveRequestId = payrollLeaveRequestId,
            IsApplied = state == FirmLeavePayrollMirrorState.Mirrored
        };

    public static string DescribeState(FirmLeavePayrollMirrorState state) => state switch
    {
        FirmLeavePayrollMirrorState.Mirrored => "Reporté en paie",
        FirmLeavePayrollMirrorState.NoPayrollEffect => "Sans effet paie",
        FirmLeavePayrollMirrorState.BlockedFrozenPayroll => "Paie du mois arrêtée",
        FirmLeavePayrollMirrorState.Failed => "Report en échec",
        FirmLeavePayrollMirrorState.Revoked => "Retiré de la paie",
        _ => "Non reporté"
    };
}
