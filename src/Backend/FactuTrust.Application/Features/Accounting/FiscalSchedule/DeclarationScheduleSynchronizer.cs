using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.FiscalSchedule;

/// <summary>
/// Synchronise l'échéancier fiscal depuis la déclaration mensuelle (appelé en best-effort par
/// <c>SaveVatDeclarationCommandHandler</c> — un échec ne doit jamais faire échouer la sauvegarde).
///
/// Règles :
/// <list type="bullet">
///   <item>l'échéance « Déclaration mensuelle » de la période est liée à la déclaration
///         (<c>SourceId</c>) et son montant estimé aligné sur le total à payer ;</item>
///   <item>déclaration soumise ⇒ échéance marquée « Déposée » (date = date de soumission ;
///         re-soumission ⇒ date actualisée) ;</item>
///   <item>rectificative (retour en brouillon) ⇒ le dépôt existant est CONSERVÉ (fait
///         historique), seul le montant est mis à jour ;</item>
///   <item>échéance absente (exercice non généré) ⇒ créée automatiquement ;</item>
///   <item>gardé par <see cref="AccountingSettings.DeclarationScheduleSyncEnabled"/> (OFF = no-op).</item>
/// </list>
/// </summary>
public sealed class DeclarationScheduleSynchronizer
{
    private readonly IFiscalScheduleRepository _schedule;
    private readonly ITunisianFiscalDeadlineService _deadlines;
    private readonly AccountingSettings _settings;
    private readonly ILogger<DeclarationScheduleSynchronizer> _logger;

    public DeclarationScheduleSynchronizer(
        IFiscalScheduleRepository schedule,
        ITunisianFiscalDeadlineService deadlines,
        IOptions<AccountingSettings> settings,
        ILogger<DeclarationScheduleSynchronizer> logger)
    {
        _schedule = schedule;
        _deadlines = deadlines;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SyncAsync(VatDeclaration declaration, decimal totalToPay, CancellationToken cancellationToken = default)
    {
        if (!_settings.DeclarationScheduleSyncEnabled)
            return;

        var submitted = declaration.Status == VatDeclarationStatus.Submitted;
        var depositDate = (declaration.SubmittedAt ?? DateTime.UtcNow).Date;
        var amount = Math.Max(0m, totalToPay);

        var entry = await _schedule.GetMonthlyDeclarationEntryAsync(declaration.Year, declaration.Month, cancellationToken);
        if (entry is null)
        {
            // Exercice non généré : l'échéance mensuelle de la période est créée depuis la déclaration.
            var create = FiscalScheduleEntry.Create(
                FiscalObligationType.MonthlyDeclaration,
                FiscalScheduleMappings.GetObligationDisplay(FiscalObligationType.MonthlyDeclaration),
                declaration.Year,
                _deadlines.ComputeVatFilingDeadline(declaration.Year, declaration.Month),
                amount,
                periodMonth: declaration.Month,
                periodStart: new DateTime(declaration.Year, declaration.Month, 1),
                periodEnd: new DateTime(declaration.Year, declaration.Month,
                    DateTime.DaysInMonth(declaration.Year, declaration.Month)),
                sourceType: FiscalScheduleSourceType.VatDeclaration,
                sourceId: declaration.Id,
                observations: "Creee automatiquement depuis la declaration mensuelle.");
            if (create.IsFailure)
            {
                _logger.LogWarning("Declaration→schedule sync: creation refused for {Year}-{Month}: {Error}",
                    declaration.Year, declaration.Month, create.Error.Description);
                return;
            }

            var created = create.Value;
            if (submitted)
                created.MarkDeposited(depositDate);
            created.SetAuditInfo("system", false);

            await _schedule.AddAsync(created, FiscalScheduleHistoryEntry.Create(
                created.Id,
                "CreatedFromDeclaration",
                $"Echeance creee depuis la declaration mensuelle V{declaration.RevisionNumber} " +
                $"(montant {amount:0.000}{(submitted ? ", deposee" : string.Empty)}).",
                null, null), cancellationToken);
            return;
        }

        entry.LinkSource(declaration.Id);

        var amountResult = entry.UpdateEstimatedAmount(amount);
        if (amountResult.IsFailure)
        {
            _logger.LogWarning("Declaration→schedule sync: amount update refused for entry {EntryId}: {Error}",
                entry.Id, amountResult.Error.Description);
            return;
        }

        if (submitted)
        {
            // Échec toléré (ex. contrainte domaine) : l'état de l'échéance prime sur la synchro.
            var deposit = entry.MarkDeposited(depositDate);
            if (deposit.IsFailure)
                _logger.LogWarning("Declaration→schedule sync: deposit refused for entry {EntryId}: {Error}",
                    entry.Id, deposit.Error.Description);
        }

        entry.SetAuditInfo("system", true);
        var summary = submitted
            ? $"Declaration mensuelle V{declaration.RevisionNumber} soumise : montant {amount:0.000}, echeance deposee le {depositDate:dd/MM/yyyy}."
            : $"Declaration mensuelle V{declaration.RevisionNumber}{(declaration.IsRectificative ? " (rectificative)" : string.Empty)} enregistree : montant {amount:0.000}.";
        await _schedule.UpdateAsync(entry, FiscalScheduleHistoryEntry.Create(
            entry.Id, "DeclarationSynced", summary, null, null), cancellationToken);
    }
}
