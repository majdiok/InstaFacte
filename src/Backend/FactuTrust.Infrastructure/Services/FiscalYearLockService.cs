using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Verrouillage définitif d'un exercice : irréversible, conditionné à des périodes toutes closes
/// et à l'absence de contrôle de pré-clôture bloquant. Aucun déverrouillage n'est exposé.
/// </summary>
public sealed class FiscalYearLockService : IFiscalYearLockService
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IPreClosingControlService _preClosing;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly AccountingSettings _settings;

    public FiscalYearLockService(
        ITenantDbContextFactory contextFactory,
        IPreClosingControlService preClosing,
        ICurrentUser currentUser,
        IAuditService auditService,
        IOptions<AccountingSettings> settings)
    {
        _contextFactory = contextFactory;
        _preClosing = preClosing;
        _currentUser = currentUser;
        _auditService = auditService;
        _settings = settings.Value;
    }

    public async Task<Result> LockYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        if (!_settings.DefinitiveLockEnabled)
            return Result.Failure(Error.Validation("Lock", "Le verrouillage définitif n'est pas activé."));
        if (fiscalYear is < 2000 or > 2100)
            return Result.Failure(Error.Validation("FiscalYear", "Exercice invalide."));

        await using var ctx = _contextFactory.CreateContext();

        if (await ctx.AccountingYearLocks.AnyAsync(l => l.FiscalYear == fiscalYear, cancellationToken))
            return Result.Failure(Error.Validation("Lock", $"L'exercice {fiscalYear} est déjà verrouillé définitivement."));

        var periods = await ctx.AccountingPeriods.AsNoTracking()
            .Where(p => p.FiscalYear == fiscalYear)
            .ToListAsync(cancellationToken);
        if (periods.Count == 0)
            return Result.Failure(Error.Validation("FiscalYear", "Aucune période comptable pour cet exercice."));

        var openPeriods = periods.Count(p => !p.IsClosed);
        if (openPeriods > 0)
            return Result.Failure(Error.Validation("Lock",
                $"{openPeriods} période(s) de l'exercice {fiscalYear} ne sont pas clôturées. Clôturez-les avant le verrouillage définitif."));

        // Contrôles de pré-clôture bloquants (mêmes que la clôture annuelle).
        if (_settings.PreClosingControlsEnabled)
        {
            var checklist = await _preClosing.RunAsync(fiscalYear, cancellationToken);
            if (checklist.IsFailure)
                return Result.Failure(checklist.Error);
            if (checklist.Value.HasBlocking)
            {
                var blocking = checklist.Value.Checks
                    .Where(c => c.Severity == (int)PreClosingSeverity.Blocking && c.Count > 0)
                    .Select(c => c.Title);
                return Result.Failure(Error.Validation("PreClosing",
                    $"Contrôles de pré-clôture bloquants : {string.Join(" ; ", blocking)}. Résolvez-les avant le verrouillage."));
            }
        }

        var lockedBy = _currentUser.Email ?? "system";
        var yearLock = AccountingYearLock.Create(fiscalYear, lockedBy);
        yearLock.SetAuditInfo(lockedBy, false);
        ctx.AccountingYearLocks.Add(yearLock);
        await ctx.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.FiscalYearLocked,
            "AccountingYearLock",
            yearLock.Id,
            newValues: new { fiscalYear, lockedBy },
            cancellationToken: cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<FiscalYearLockDto>>> GetLocksAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var rows = await ctx.AccountingYearLocks.AsNoTracking()
            .OrderByDescending(l => l.FiscalYear)
            .Select(l => new FiscalYearLockDto { FiscalYear = l.FiscalYear, LockedAt = l.LockedAt, LockedBy = l.LockedBy })
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<FiscalYearLockDto>>(rows);
    }
}
