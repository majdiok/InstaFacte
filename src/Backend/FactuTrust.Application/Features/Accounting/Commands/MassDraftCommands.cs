using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Commands;

/// <summary>
/// Édition de masse d'écritures EN BROUILLON (journal / date / libellé) — typiquement pour corriger
/// un lot importé avant validation. Ne touche JAMAIS une écriture validée ou clôturée : ces ids sont
/// ignorés et comptés à part. Chaque écriture est mutée atomiquement (une transaction par entrée).
/// </summary>
public sealed record MassUpdateDraftEntriesCommand(
    IReadOnlyList<Guid> Ids,
    string? NewJournalCode,
    DateTime? NewDate,
    string? NewLabel) : IRequest<Result<MassDraftUpdateResultDto>>;

public sealed class MassUpdateDraftEntriesCommandHandler
    : IRequestHandler<MassUpdateDraftEntriesCommand, Result<MassDraftUpdateResultDto>>
{
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IJournalRepository _journals;
    private readonly IAccountingPeriodService _periodService;
    private readonly IAuditService _auditService;

    public MassUpdateDraftEntriesCommandHandler(
        IJournalEntryRepository journalEntries,
        IJournalRepository journals,
        IAccountingPeriodService periodService,
        IAuditService auditService)
    {
        _journalEntries = journalEntries;
        _journals = journals;
        _periodService = periodService;
        _auditService = auditService;
    }

    public async Task<Result<MassDraftUpdateResultDto>> Handle(MassUpdateDraftEntriesCommand request, CancellationToken cancellationToken)
    {
        if (request.Ids is null || request.Ids.Count == 0)
            return Result.Failure<MassDraftUpdateResultDto>(Error.Validation("Ids", "Sélectionnez au moins une écriture."));

        var newJournal = string.IsNullOrWhiteSpace(request.NewJournalCode) ? null : request.NewJournalCode.Trim().ToUpperInvariant();
        var newLabel = string.IsNullOrWhiteSpace(request.NewLabel) ? null : request.NewLabel.Trim();
        var newDate = request.NewDate?.Date;

        if (newJournal is null && newLabel is null && newDate is null)
            return Result.Failure<MassDraftUpdateResultDto>(Error.Validation("Changes", "Aucune modification demandée."));

        // Contrôle du journal cible une seule fois (catalogue) — un journal inconnu invalide tout le lot.
        if (newJournal is not null && !await _journals.ExistsActiveJournalCodeAsync(newJournal, cancellationToken))
            return Result.Failure<MassDraftUpdateResultDto>(Error.Validation("JournalCode", $"Le journal {newJournal} est inconnu ou inactif."));

        // Période cible de la nouvelle date, résolue une seule fois : une période clôturée rend
        // l'opération impossible (on ne déplace pas un brouillon dans une période close).
        Guid? targetPeriodId = null;
        if (newDate is not null)
        {
            var period = await _periodService.EnsureOpenPeriodAsync(newDate.Value, cancellationToken);
            if (period.IsFailure)
                return Result.Failure<MassDraftUpdateResultDto>(period.Error);
            targetPeriodId = period.Value.Id;
        }

        var updated = 0;
        var skipped = 0;
        foreach (var id in request.Ids.Distinct())
        {
            var result = await _journalEntries.MutateAsync(id, entry =>
            {
                if (!entry.IsDraft)
                    return Result.Failure(Error.Validation("Status", "Non-brouillon ignoré."));
                if (entry.AccountingPeriod?.IsClosed == true)
                    return Result.Failure(Error.Validation("Period", "Période clôturée ignorée."));
                // Une extourne doit rester le miroir de son origine : jamais éditée en masse.
                if (entry.ReversesEntryId is not null)
                    return Result.Failure(Error.Validation("Extourne", "Extourne ignorée."));
                // R-07/R-08 : les écritures paie sont exclues de l'édition de masse (même cabinet).
                if (PayrollSourcedEntryGuard.IsSystemSource(entry.SourceEntityType))
                    return Result.Failure(Error.Validation("Source", "Écriture paie ignorée."));

                if (newJournal is not null)
                {
                    var r = entry.ChangeDraftJournal(newJournal);
                    if (r.IsFailure) return r;
                }
                if (newDate is not null)
                {
                    var r = entry.ChangeDraftDate(newDate.Value, targetPeriodId!.Value);
                    if (r.IsFailure) return r;
                }
                if (newLabel is not null)
                {
                    var r = entry.ChangeDraftLabel(newLabel);
                    if (r.IsFailure) return r;
                }
                return Result.Success();
            }, cancellationToken);

            if (result.IsSuccess) updated++;
            else skipped++;
        }

        await _auditService.LogAsync(
            AuditActions.Accounting.DraftUpdated,
            "JournalEntry",
            newValues: new { Mass = true, request.Ids.Count, Updated = updated, Skipped = skipped, newJournal, newDate, HasNewLabel = newLabel is not null },
            cancellationToken: cancellationToken);

        return Result.Success(new MassDraftUpdateResultDto { Updated = updated, Skipped = skipped });
    }
}

/// <summary>
/// Suppression de masse d'écritures EN BROUILLON. Ignore (compte à part) tout id introuvable,
/// non-brouillon ou en période clôturée. Les brouillons d'extourne sont ignorés (leur suppression
/// unitaire restaure l'origine — logique non dupliquée ici pour rester sûr).
/// </summary>
public sealed record MassDeleteDraftEntriesCommand(IReadOnlyList<Guid> Ids) : IRequest<Result<MassDraftDeleteResultDto>>;

public sealed class MassDeleteDraftEntriesCommandHandler
    : IRequestHandler<MassDeleteDraftEntriesCommand, Result<MassDraftDeleteResultDto>>
{
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IAuditService _auditService;

    public MassDeleteDraftEntriesCommandHandler(IJournalEntryRepository journalEntries, IAuditService auditService)
    {
        _journalEntries = journalEntries;
        _auditService = auditService;
    }

    public async Task<Result<MassDraftDeleteResultDto>> Handle(MassDeleteDraftEntriesCommand request, CancellationToken cancellationToken)
    {
        if (request.Ids is null || request.Ids.Count == 0)
            return Result.Failure<MassDraftDeleteResultDto>(Error.Validation("Ids", "Sélectionnez au moins une écriture."));

        var deleted = 0;
        var skipped = 0;
        foreach (var id in request.Ids.Distinct())
        {
            var entry = await _journalEntries.GetByIdAsync(id, cancellationToken);
            if (entry is null || !entry.IsDraft || entry.AccountingPeriod?.IsClosed == true || entry.ReversesEntryId is not null
                || PayrollSourcedEntryGuard.IsSystemSource(entry.SourceEntityType))
            {
                skipped++;
                continue;
            }

            await _journalEntries.RemoveAsync(entry, cancellationToken);
            deleted++;
        }

        await _auditService.LogAsync(
            AuditActions.Accounting.DraftDeleted,
            "JournalEntry",
            oldValues: new { Mass = true, request.Ids.Count, Deleted = deleted, Skipped = skipped },
            cancellationToken: cancellationToken);

        return Result.Success(new MassDraftDeleteResultDto { Deleted = deleted, Skipped = skipped });
    }
}
