using FactuTrust.Application.Accounting;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Commands;

/// <summary>Enregistre la personnalisation d'une note annexe (titre, texte narratif, masquage).</summary>
public sealed record UpsertNctNoteOverrideCommand(UpsertNctNoteOverrideRequest Request)
    : IRequest<Result<NctNoteOverrideDto>>;

public sealed class UpsertNctNoteOverrideCommandHandler
    : IRequestHandler<UpsertNctNoteOverrideCommand, Result<NctNoteOverrideDto>>
{
    private readonly INctNoteOverrideRepository _repository;
    private readonly IAuditService _auditService;

    public UpsertNctNoteOverrideCommandHandler(INctNoteOverrideRepository repository, IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Result<NctNoteOverrideDto>> Handle(UpsertNctNoteOverrideCommand command, CancellationToken cancellationToken)
    {
        var r = command.Request;

        // Le numéro doit exister au catalogue : personnaliser une note fantôme n'aurait aucun effet
        // visible et laisserait une ligne orpheline.
        if (!NctDetailedNoteCatalog.All.Any(d => d.Number == r.NoteNumber))
            return Result.Failure<NctNoteOverrideDto>(
                Error.Validation("NoteNumber", $"La note {r.NoteNumber} n'existe pas au catalogue des annexes."));

        var result = await _repository.UpsertAsync(
            r.FiscalYear, r.NoteNumber, r.CustomTitle, r.CustomDescription, r.IsHidden, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<NctNoteOverrideDto>(result.Error);

        await _auditService.LogAsync(
            AuditActions.Accounting.NctNoteOverrideChanged,
            "NctNoteOverride",
            result.Value.Id,
            newValues: new { r.FiscalYear, r.NoteNumber, r.IsHidden, HasTitle = r.CustomTitle is not null },
            cancellationToken: cancellationToken);

        var row = result.Value;
        return Result.Success(new NctNoteOverrideDto
        {
            FiscalYear = row.FiscalYear,
            NoteNumber = row.NoteNumber,
            CustomTitle = row.CustomTitle,
            CustomDescription = row.CustomDescription,
            IsHidden = row.IsHidden
        });
    }
}

/// <summary>« Rétablir » : supprime la personnalisation, la note reprend le libellé du catalogue.</summary>
public sealed record DeleteNctNoteOverrideCommand(int FiscalYear, int NoteNumber) : IRequest<Result>;

public sealed class DeleteNctNoteOverrideCommandHandler : IRequestHandler<DeleteNctNoteOverrideCommand, Result>
{
    private readonly INctNoteOverrideRepository _repository;
    private readonly IAuditService _auditService;

    public DeleteNctNoteOverrideCommandHandler(INctNoteOverrideRepository repository, IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeleteNctNoteOverrideCommand command, CancellationToken cancellationToken)
    {
        if (command.FiscalYear is < 2000 or > 2100)
            return Result.Failure(Error.Validation("FiscalYear", "Exercice invalide."));

        await _repository.DeleteAsync(command.FiscalYear, command.NoteNumber, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Accounting.NctNoteOverrideChanged,
            "NctNoteOverride",
            oldValues: new { command.FiscalYear, command.NoteNumber, Reset = true },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
