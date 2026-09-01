using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

/// <summary>
/// Fixe le profil d'imputation comptable de la paie du dossier. Opération réservée au cabinet
/// délégué (<c>PayrollFirmExclusiveRequests</c>) : elle détermine les comptes de toutes les
/// écritures de paie à venir.
/// </summary>
public sealed record UpdatePayrollAccountingSettingsCommand(UpdatePayrollAccountingSettingsRequest Request)
    : IRequest<Result<PayrollAccountingSettingsDto>>;

public sealed class UpdatePayrollAccountingSettingsCommandHandler
    : IRequestHandler<UpdatePayrollAccountingSettingsCommand, Result<PayrollAccountingSettingsDto>>
{
    private readonly IPayrollAccountingSettingsRepository _repository;
    private readonly IPayrollAccountingProfileResolver _profileResolver;
    private readonly IPayrollRunRepository _runs;
    private readonly ICurrentUser _currentUser;

    public UpdatePayrollAccountingSettingsCommandHandler(
        IPayrollAccountingSettingsRepository repository,
        IPayrollAccountingProfileResolver profileResolver,
        IPayrollRunRepository runs,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _profileResolver = profileResolver;
        _runs = runs;
        _currentUser = currentUser;
    }

    public async Task<Result<PayrollAccountingSettingsDto>> Handle(
        UpdatePayrollAccountingSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Request;

        if (!Enum.TryParse<PayrollAccountProfile>(dto.AccountProfile, ignoreCase: true, out var profile)
            || !Enum.IsDefined(profile))
        {
            return Result.Failure<PayrollAccountingSettingsDto>(Error.Validation(
                "AccountProfile",
                "Profil d'imputation inconnu : attendu « Legacy » ou « Sce2026 »."));
        }

        var effectiveDate = dto.AccountProfileEffectiveDate?.Date;

        // La bascule ne doit jamais couvrir un cycle déjà arrêté : rouvrir puis revalider ce cycle
        // régénérerait son OD avec une autre cartographie de comptes, changeant rétroactivement une
        // période déjà déclarée. Le domaine impose déjà « date obligatoire sous Sce2026 » et
        // « premier du mois » ; c'est ici, avec accès aux cycles, qu'on borne la date au passé arrêté.
        var runs = await _runs.ListAsync(null, cancellationToken);
        var lastSettled = PayrollAccountingSettingsBoundaries.LastSettled(runs);
        var earliest = PayrollAccountingSettingsBoundaries.EarliestEffectiveDate(lastSettled);

        if (effectiveDate is { } chosen && earliest is { } floor && chosen < floor)
        {
            return Result.Failure<PayrollAccountingSettingsDto>(Error.Validation(
                "AccountProfileEffectiveDate",
                $"La date de bascule doit être postérieure au dernier cycle arrêté "
                + $"({lastSettled!.Value.Month:D2}/{lastSettled.Value.Year}) : la première date "
                + $"acceptable est le {floor:dd/MM/yyyy}."));
        }

        var upsert = await _repository.UpsertAsync(
            profile,
            effectiveDate,
            dto.InKindOffsetAccount,
            dto.DisbursementEntriesEnabled,
            dto.DetailedSalarySplitEnabled,
            dto.EmployeeAuxiliaryEnabled,
            _currentUser.UserId?.ToString() ?? _currentUser.Email ?? "system",
            cancellationToken);

        if (upsert.IsFailure)
            return Result.Failure<PayrollAccountingSettingsDto>(upsert.Error);

        // Relecture par le résolveur : la réponse décrit ce qui sera réellement appliqué (réglage
        // du dossier superposé à la configuration globale), et non l'écho de la requête.
        var snapshot = await _profileResolver.GetAsync(cancellationToken);

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
            EarliestEffectiveDate = earliest
        });
    }
}
