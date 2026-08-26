using CashRegisterEntity = FactuTrust.Domain.Entities.CashRegister;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.CashRegister;

public sealed record ListCashRegistersQuery(Guid WarehouseId) : IRequest<Result<IReadOnlyList<CashRegisterDto>>>;

public sealed record CreateCashRegisterCommand(CreateCashRegisterRequest Request)
    : IRequest<Result<CashRegisterDto>>;

public sealed class ListCashRegistersQueryHandler
    : IRequestHandler<ListCashRegistersQuery, Result<IReadOnlyList<CashRegisterDto>>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly ICurrentUser _currentUser;
    private readonly bool _requireOpenSession;

    public ListCashRegistersQueryHandler(
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        ICurrentUser currentUser,
        IOptions<CashDeskFeaturesOptions> features)
    {
        _warehouses = warehouses;
        _registers = registers;
        _currentUser = currentUser;
        _requireOpenSession = features.Value.PosRegisterSessions;
    }

    public async Task<Result<IReadOnlyList<CashRegisterDto>>> Handle(
        ListCashRegistersQuery request,
        CancellationToken cancellationToken)
    {
        var ensured = await CashRegisterProvisioning.EnsureForWarehouseAsync(
            _warehouses, _registers, request.WarehouseId, _currentUser.UserId?.ToString(), cancellationToken);
        if (ensured.IsFailure)
            return Result.Failure<IReadOnlyList<CashRegisterDto>>(ensured.Error);

        var list = await _registers.ListByWarehouseIdAsync(request.WarehouseId, cancellationToken);
        return Result.Success<IReadOnlyList<CashRegisterDto>>(
            list.Select(r => GetCashRegisterQueryHandler.Map(r, _requireOpenSession)).ToList());
    }
}

public sealed class CreateCashRegisterCommandHandler
    : IRequestHandler<CreateCashRegisterCommand, Result<CashRegisterDto>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly ICurrentUser _currentUser;
    private readonly bool _requireOpenSession;

    public CreateCashRegisterCommandHandler(
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        ICurrentUser currentUser,
        IOptions<CashDeskFeaturesOptions> features)
    {
        _warehouses = warehouses;
        _registers = registers;
        _currentUser = currentUser;
        _requireOpenSession = features.Value.PosRegisterSessions;
    }

    public async Task<Result<CashRegisterDto>> Handle(
        CreateCashRegisterCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var warehouse = await _warehouses.GetByIdAsync(request.WarehouseId, cancellationToken);
        if (warehouse is null)
            return Result.Failure<CashRegisterDto>(Error.NotFound("Warehouse", request.WarehouseId));
        if (!warehouse.IsActive)
            return Result.Failure<CashRegisterDto>(
                Error.Validation("Warehouse", "Cet entrepôt est désactivé"));

        var existing = await _registers.ListByWarehouseIdAsync(request.WarehouseId, cancellationToken);
        var makeDefault = request.IsDefault || existing.Count == 0;

        if (await _registers.CodeExistsAsync(request.Code, null, cancellationToken))
            return Result.Failure<CashRegisterDto>(
                Error.Validation("Code", "Ce code caisse existe déjà"));

        var created = CashRegisterEntity.Create(
            request.Code,
            request.Name,
            request.WarehouseId,
            makeDefault);
        if (created.IsFailure)
            return Result.Failure<CashRegisterDto>(created.Error);

        created.Value.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        var saved = await _registers.AddAsync(created.Value, cancellationToken);

        if (makeDefault)
        {
            await CashRegisterProvisioning.ClearOtherDefaultsAsync(
                _registers, request.WarehouseId, saved.Id, cancellationToken);
        }

        return Result.Success(GetCashRegisterQueryHandler.Map(saved, _requireOpenSession));
    }
}
