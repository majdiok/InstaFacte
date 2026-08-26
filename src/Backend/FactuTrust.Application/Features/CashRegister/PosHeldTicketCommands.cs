using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.CashRegister;

public sealed record ListPosHeldTicketsQuery(Guid WarehouseId, Guid? CashRegisterId = null)
    : IRequest<Result<IReadOnlyList<PosHeldTicketDto>>>;

public sealed record SavePosHeldTicketCommand(SavePosHeldTicketRequest Request) : IRequest<Result<PosHeldTicketDto>>;

public sealed record RecallPosHeldTicketCommand(Guid TicketId) : IRequest<Result<PosHeldTicketDto>>;

public sealed record DeletePosHeldTicketCommand(Guid TicketId) : IRequest<Result>;

public sealed record ImportPosHeldTicketsCommand(ImportPosHeldTicketsRequest Request) : IRequest<Result<int>>;

public sealed class ListPosHeldTicketsQueryHandler
    : IRequestHandler<ListPosHeldTicketsQuery, Result<IReadOnlyList<PosHeldTicketDto>>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly IPosHeldTicketRepository _tickets;
    private readonly ICurrentUser _currentUser;

    public ListPosHeldTicketsQueryHandler(
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        IPosHeldTicketRepository tickets,
        ICurrentUser currentUser)
    {
        _warehouses = warehouses;
        _registers = registers;
        _tickets = tickets;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<PosHeldTicketDto>>> Handle(
        ListPosHeldTicketsQuery request,
        CancellationToken cancellationToken)
    {
        var ensured = await CashRegisterResolver.ResolveAsync(
            _warehouses, _registers, request.WarehouseId, request.CashRegisterId, _currentUser.UserId?.ToString(), cancellationToken);
        if (ensured.IsFailure)
            return Result.Failure<IReadOnlyList<PosHeldTicketDto>>(ensured.Error);

        var items = await _tickets.ListByRegisterAsync(ensured.Value.Id, cancellationToken);
        return Result.Success<IReadOnlyList<PosHeldTicketDto>>(items.Select(Map).ToList());
    }

    internal static PosHeldTicketDto Map(PosHeldTicket ticket)
    {
        object? state = null;
        try
        {
            using var doc = JsonDocument.Parse(ticket.StateJson);
            state = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            state = null;
        }

        return new PosHeldTicketDto
        {
            Id = ticket.Id,
            Label = ticket.Label,
            TotalTtc = ticket.TotalTtc,
            LineCount = ticket.LineCount,
            HeldAt = ticket.HeldAt,
            State = state
        };
    }
}

public sealed class SavePosHeldTicketCommandHandler : IRequestHandler<SavePosHeldTicketCommand, Result<PosHeldTicketDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly ICashRegisterSessionRepository _sessions;
    private readonly IPosHeldTicketRepository _tickets;

    public SavePosHeldTicketCommandHandler(
        ICurrentUser currentUser,
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        ICashRegisterSessionRepository sessions,
        IPosHeldTicketRepository tickets)
    {
        _currentUser = currentUser;
        _warehouses = warehouses;
        _registers = registers;
        _sessions = sessions;
        _tickets = tickets;
    }

    public async Task<Result<PosHeldTicketDto>> Handle(SavePosHeldTicketCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            return Result.Failure<PosHeldTicketDto>(Error.Unauthorized("Utilisateur non identifié"));

        var request = command.Request;
        if (request.WarehouseId is not { } warehouseId || warehouseId == Guid.Empty)
            return Result.Failure<PosHeldTicketDto>(Error.Validation("WarehouseId", "L'entrepôt est obligatoire"));

        if (request.State.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return Result.Failure<PosHeldTicketDto>(Error.Validation("State", "L'état du ticket est obligatoire"));

        var ensured = await CashRegisterResolver.ResolveAsync(
            _warehouses, _registers, warehouseId, request.CashRegisterId, userId.ToString(), cancellationToken);
        if (ensured.IsFailure)
            return Result.Failure<PosHeldTicketDto>(ensured.Error);

        var open = await _sessions.GetOpenByRegisterIdAsync(ensured.Value.Id, cancellationToken);
        var created = PosHeldTicket.Create(
            ensured.Value.Id,
            userId,
            request.Label,
            request.TotalTtc,
            request.LineCount,
            request.State.GetRawText(),
            open?.Id,
            request.Id);

        if (created.IsFailure)
            return Result.Failure<PosHeldTicketDto>(created.Error);

        created.Value.SetAuditInfo(userId.ToString());
        var saved = await _tickets.AddAsync(created.Value, cancellationToken);
        return Result.Success(ListPosHeldTicketsQueryHandler.Map(saved));
    }
}

public sealed class RecallPosHeldTicketCommandHandler : IRequestHandler<RecallPosHeldTicketCommand, Result<PosHeldTicketDto>>
{
    private readonly IPosHeldTicketRepository _tickets;

    public RecallPosHeldTicketCommandHandler(IPosHeldTicketRepository tickets)
    {
        _tickets = tickets;
    }

    public async Task<Result<PosHeldTicketDto>> Handle(RecallPosHeldTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _tickets.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result.Failure<PosHeldTicketDto>(Error.NotFound("PosHeldTicket", request.TicketId));

        var dto = ListPosHeldTicketsQueryHandler.Map(ticket);
        await _tickets.DeleteAsync(ticket, cancellationToken);
        return Result.Success(dto);
    }
}

public sealed class DeletePosHeldTicketCommandHandler : IRequestHandler<DeletePosHeldTicketCommand, Result>
{
    private readonly IPosHeldTicketRepository _tickets;

    public DeletePosHeldTicketCommandHandler(IPosHeldTicketRepository tickets)
    {
        _tickets = tickets;
    }

    public async Task<Result> Handle(DeletePosHeldTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _tickets.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result.Success();
        await _tickets.DeleteAsync(ticket, cancellationToken);
        return Result.Success();
    }
}

public sealed class ImportPosHeldTicketsCommandHandler : IRequestHandler<ImportPosHeldTicketsCommand, Result<int>>
{
    private readonly IMediator _mediator;

    public ImportPosHeldTicketsCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<Result<int>> Handle(ImportPosHeldTicketsCommand command, CancellationToken cancellationToken)
    {
        var imported = 0;
        foreach (var ticket in command.Request.Tickets)
        {
            var withWarehouse = ticket with { WarehouseId = command.Request.WarehouseId };
            var saved = await _mediator.Send(new SavePosHeldTicketCommand(withWarehouse), cancellationToken);
            if (saved.IsSuccess)
                imported++;
        }

        return Result.Success(imported);
    }
}
