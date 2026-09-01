using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

/// <summary>
/// Réglage d'imputation comptable de la paie du dossier, tel qu'il sera effectivement appliqué
/// (réglage propre du dossier, sinon configuration globale), enrichi des bornes que l'écran doit
/// respecter pour proposer une date de bascule valide.
/// </summary>
public sealed record GetPayrollAccountingSettingsQuery : IRequest<Result<PayrollAccountingSettingsDto>>;

public sealed class GetPayrollAccountingSettingsQueryHandler
    : IRequestHandler<GetPayrollAccountingSettingsQuery, Result<PayrollAccountingSettingsDto>>
{
    private readonly IPayrollAccountingProfileResolver _profileResolver;
    private readonly IPayrollRunRepository _runs;

    public GetPayrollAccountingSettingsQueryHandler(
        IPayrollAccountingProfileResolver profileResolver,
        IPayrollRunRepository runs)
    {
        _profileResolver = profileResolver;
        _runs = runs;
    }

    public async Task<Result<PayrollAccountingSettingsDto>> Handle(
        GetPayrollAccountingSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _profileResolver.GetAsync(cancellationToken);
        var runs = await _runs.ListAsync(null, cancellationToken);
        var lastSettled = PayrollAccountingSettingsBoundaries.LastSettled(runs);

        return Result.Success(new PayrollAccountingSettingsDto
        {
            AccountProfile = snapshot.Profile.ToString(),
            AccountProfileEffectiveDate = snapshot.EffectiveDate,
            InKindOffsetAccount = snapshot.InKindOffsetAccount,
            DisbursementEntriesEnabled = snapshot.DisbursementEntriesEnabled,
            DetailedSalarySplitEnabled = snapshot.DetailedSalarySplitEnabled,
            EmployeeAuxiliaryEnabled = snapshot.EmployeeAuxiliaryEnabled,
            IsTenantOverride = snapshot.IsTenantOverride,
            LastSettledPeriod = lastSettled is { } p ? $"{p.Year:D4}-{p.Month:D2}" : null,
            EarliestEffectiveDate = PayrollAccountingSettingsBoundaries.EarliestEffectiveDate(lastSettled)
        });
    }
}

/// <summary>
/// Bornes partagées par la lecture et l'écriture du réglage : la date de bascule ne peut pas
/// couvrir un cycle déjà arrêté, sinon rouvrir puis revalider ce cycle en changerait l'imputation.
/// Un seul calcul, donc aucun écart possible entre ce que l'écran propose et ce que le serveur accepte.
/// </summary>
public static class PayrollAccountingSettingsBoundaries
{
    /// <summary>Période du dernier cycle validé ou clôturé, ou <c>null</c> s'il n'y en a aucun.</summary>
    public static (int Year, int Month)? LastSettled(IReadOnlyList<PayrollRun> runs)
    {
        var settled = runs
            .Where(r => r.Status is PayrollRunStatus.Validated or PayrollRunStatus.Closed)
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Month)
            .FirstOrDefault();

        return settled is null ? null : (settled.Year, settled.Month);
    }

    /// <summary>Première date de bascule acceptable : 1er du mois suivant le dernier cycle arrêté.</summary>
    public static DateTime? EarliestEffectiveDate((int Year, int Month)? lastSettled) =>
        lastSettled is { } p ? new DateTime(p.Year, p.Month, 1).AddMonths(1) : null;
}
