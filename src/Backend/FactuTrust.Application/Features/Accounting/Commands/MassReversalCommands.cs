using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Commands;

/// <summary>
/// Correction de masse d'écritures VALIDÉES par extourne.
/// <para>
/// Une écriture validée ne se modifie jamais : elle s'extourne (écriture miroir) puis se ressaisit.
/// Cette commande est donc une PURE DÉLÉGATION à
/// <c>IAccountingService.ReverseJournalEntryAsync</c>, dont elle hérite intégralement les gardes :
/// flag <c>ManualReversalEnabled</c>, refus des brouillons, refus d'une écriture déjà extournée,
/// repli de période si celle d'origine est clôturée. Aucune logique comptable n'est réécrite ici.
/// </para>
/// <para>
/// Un identifiant en échec est COMPTÉ À PART, jamais bloquant pour le reste du lot.
/// </para>
/// </summary>
public sealed record MassReverseEntriesCommand(IReadOnlyList<Guid> Ids, string Reason)
    : IRequest<Result<MassReversalResultDto>>;

public sealed class MassReverseEntriesCommandHandler
    : IRequestHandler<MassReverseEntriesCommand, Result<MassReversalResultDto>>
{
    private readonly IAccountingService _accountingService;
    private readonly IAuditService _auditService;

    public MassReverseEntriesCommandHandler(IAccountingService accountingService, IAuditService auditService)
    {
        _accountingService = accountingService;
        _auditService = auditService;
    }

    public async Task<Result<MassReversalResultDto>> Handle(MassReverseEntriesCommand command, CancellationToken cancellationToken)
    {
        if (command.Ids is null || command.Ids.Count == 0)
            return Result.Failure<MassReversalResultDto>(Error.Validation("Ids", "Sélectionnez au moins une écriture."));

        // Une correction comptable non justifiée n'a pas lieu d'être : le motif est repris dans le
        // libellé de chaque extourne et constitue la trace de la décision.
        var reason = command.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(reason))
            return Result.Failure<MassReversalResultDto>(Error.Validation("Reason", "Le motif de correction est obligatoire."));

        var reversalIds = new List<Guid>();
        var skipped = 0;

        foreach (var id in command.Ids.Distinct())
        {
            var result = await _accountingService.ReverseJournalEntryAsync(id, reason, cancellationToken);
            if (result.IsSuccess)
                reversalIds.Add(result.Value);
            else
                skipped++;
        }

        await _auditService.LogAsync(
            AuditActions.Accounting.EntryReversed,
            "JournalEntry",
            newValues: new { Mass = true, Requested = command.Ids.Count, Reversed = reversalIds.Count, Skipped = skipped, Reason = reason },
            cancellationToken: cancellationToken);

        return Result.Success(new MassReversalResultDto
        {
            Reversed = reversalIds.Count,
            Skipped = skipped,
            ReversalEntryIds = reversalIds
        });
    }
}
