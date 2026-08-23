using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Commands;

public sealed record DeleteSalesReturnNoteCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteSalesReturnNoteCommandHandler : IRequestHandler<DeleteSalesReturnNoteCommand, Result>
{
    private readonly ISalesReturnNoteRepository _returnNoteRepository;
    private readonly IAuditService _auditService;

    public DeleteSalesReturnNoteCommandHandler(
        ISalesReturnNoteRepository returnNoteRepository,
        IAuditService auditService)
    {
        _returnNoteRepository = returnNoteRepository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeleteSalesReturnNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _returnNoteRepository.GetByIdAsync(request.Id, cancellationToken);
        if (note is null)
            return Result.Failure(Error.NotFound("SalesReturnNote", request.Id));

        if (!note.Status.CanBeDeleted())
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être supprimés."));

        var number = note.Number.Value;
        await _returnNoteRepository.DeleteAsync(note, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SalesReturnNote.Deleted,
            "SalesReturnNote",
            note.Id,
            newValues: new { Number = number },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
