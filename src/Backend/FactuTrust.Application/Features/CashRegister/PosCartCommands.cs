using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.CashRegister;

public sealed record GetPosCartDraftQuery(Guid? WarehouseId) : IRequest<Result<PosCartStateDto>>;

public sealed record SavePosCartDraftCommand(Guid? WarehouseId, JsonElement State) : IRequest<Result>;

public sealed record ClearPosCartDraftCommand(Guid? WarehouseId) : IRequest<Result>;

public sealed class GetPosCartDraftQueryHandler : IRequestHandler<GetPosCartDraftQuery, Result<PosCartStateDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly IPosCartDraftRepository _drafts;

    public GetPosCartDraftQueryHandler(
        ICurrentUser currentUser,
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        IPosCartDraftRepository drafts)
    {
        _currentUser = currentUser;
        _warehouses = warehouses;
        _registers = registers;
        _drafts = drafts;
    }

    public async Task<Result<PosCartStateDto>> Handle(GetPosCartDraftQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            return Result.Failure<PosCartStateDto>(Error.Unauthorized("Utilisateur non identifié"));

        PosCartDraft? draft;
        if (request.WarehouseId is { } warehouseId && warehouseId != Guid.Empty)
        {
            var register = await _registers.GetByWarehouseIdAsync(warehouseId, cancellationToken);
            if (register is null)
            {
                var ensured = await CashRegisterProvisioning.EnsureForWarehouseAsync(
                    _warehouses, _registers, warehouseId, userId.ToString(), cancellationToken);
                if (ensured.IsFailure)
                    return Result.Failure<PosCartStateDto>(ensured.Error);
                register = ensured.Value;
            }

            draft = await _drafts.GetByUserAndRegisterAsync(userId, register.Id, cancellationToken);
        }
        else
        {
            draft = await _drafts.GetLatestByUserAsync(userId, cancellationToken);
        }

        if (draft is null || string.IsNullOrWhiteSpace(draft.StateJson))
            return Result.Success(new PosCartStateDto { State = null });

        using var doc = JsonDocument.Parse(draft.StateJson);
        return Result.Success(new PosCartStateDto { State = doc.RootElement.Clone() });
    }
}

public sealed class SavePosCartDraftCommandHandler : IRequestHandler<SavePosCartDraftCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly IPosCartDraftRepository _drafts;

    public SavePosCartDraftCommandHandler(
        ICurrentUser currentUser,
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        IPosCartDraftRepository drafts)
    {
        _currentUser = currentUser;
        _warehouses = warehouses;
        _registers = registers;
        _drafts = drafts;
    }

    public async Task<Result> Handle(SavePosCartDraftCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            return Result.Failure(Error.Unauthorized("Utilisateur non identifié"));

        if (request.State.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return Result.Failure(Error.Validation("State", "État POS manquant"));

        if (request.WarehouseId is not { } warehouseId || warehouseId == Guid.Empty)
        {
            var fallback = await _warehouses.GetDefaultAsync(cancellationToken);
            if (fallback is null)
                return Result.Failure(Error.Validation("WarehouseId", "L'entrepôt est obligatoire pour enregistrer le panier"));
            warehouseId = fallback.Id;
        }

        var ensured = await CashRegisterProvisioning.EnsureForWarehouseAsync(
            _warehouses, _registers, warehouseId, userId.ToString(), cancellationToken);
        if (ensured.IsFailure)
            return Result.Failure(ensured.Error);

        var json = request.State.GetRawText();
        var existing = await _drafts.GetByUserAndRegisterAsync(userId, ensured.Value.Id, cancellationToken);
        if (existing is null)
        {
            var created = PosCartDraft.Create(userId, ensured.Value.Id, json);
            if (created.IsFailure)
                return created;
            created.Value.SetAuditInfo(userId.ToString());
            await _drafts.AddAsync(created.Value, cancellationToken);
            return Result.Success();
        }

        var replaced = existing.ReplaceState(json);
        if (replaced.IsFailure)
            return replaced;
        existing.SetAuditInfo(userId.ToString(), isUpdate: true);
        await _drafts.UpdateAsync(existing, cancellationToken);
        return Result.Success();
    }
}

public sealed class ClearPosCartDraftCommandHandler : IRequestHandler<ClearPosCartDraftCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly ICashRegisterRepository _registers;
    private readonly IPosCartDraftRepository _drafts;

    public ClearPosCartDraftCommandHandler(
        ICurrentUser currentUser,
        ICashRegisterRepository registers,
        IPosCartDraftRepository drafts)
    {
        _currentUser = currentUser;
        _registers = registers;
        _drafts = drafts;
    }

    public async Task<Result> Handle(ClearPosCartDraftCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            return Result.Success();

        if (request.WarehouseId is { } warehouseId && warehouseId != Guid.Empty)
        {
            var register = await _registers.GetByWarehouseIdAsync(warehouseId, cancellationToken);
            if (register is not null)
                await _drafts.DeleteByUserAndRegisterAsync(userId, register.Id, cancellationToken);
            return Result.Success();
        }

        var latest = await _drafts.GetLatestByUserAsync(userId, cancellationToken);
        if (latest is not null)
            await _drafts.DeleteAsync(latest, cancellationToken);
        return Result.Success();
    }
}
