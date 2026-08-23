using CashRegisterEntity = FactuTrust.Domain.Entities.CashRegister;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Application.Features.CashRegister;

public sealed record GetCashRegisterQuery(Guid WarehouseId) : IRequest<Result<CashRegisterDto>>;

public sealed record GetOpenCashRegisterSessionQuery(Guid WarehouseId)
    : IRequest<Result<CashRegisterSessionDto?>>;

public sealed record OpenCashRegisterSessionCommand(OpenCashRegisterSessionRequest Request)
    : IRequest<Result<CashRegisterSessionDto>>;

public sealed class GetCashRegisterQueryHandler : IRequestHandler<GetCashRegisterQuery, Result<CashRegisterDto>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly ICurrentUser _currentUser;
    private readonly bool _requireOpenSession;

    public GetCashRegisterQueryHandler(
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

    public async Task<Result<CashRegisterDto>> Handle(GetCashRegisterQuery request, CancellationToken cancellationToken)
    {
        var ensured = await CashRegisterProvisioning.EnsureForWarehouseAsync(
            _warehouses, _registers, request.WarehouseId, _currentUser.UserId?.ToString(), cancellationToken);
        if (ensured.IsFailure)
            return Result.Failure<CashRegisterDto>(ensured.Error);

        return Result.Success(Map(ensured.Value, _requireOpenSession));
    }

    internal static CashRegisterDto Map(CashRegisterEntity register, bool requireOpenSession) => new()
    {
        Id = register.Id,
        Code = register.Code,
        Name = register.Name,
        WarehouseId = register.WarehouseId,
        IsActive = register.IsActive,
        RequireOpenSession = requireOpenSession
    };
}

public sealed class GetOpenCashRegisterSessionQueryHandler
    : IRequestHandler<GetOpenCashRegisterSessionQuery, Result<CashRegisterSessionDto?>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly ICashRegisterSessionRepository _sessions;
    private readonly IZReportRepository _zReports;
    private readonly ICurrentUser _currentUser;

    public GetOpenCashRegisterSessionQueryHandler(
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        ICashRegisterSessionRepository sessions,
        IZReportRepository zReports,
        ICurrentUser currentUser)
    {
        _warehouses = warehouses;
        _registers = registers;
        _sessions = sessions;
        _zReports = zReports;
        _currentUser = currentUser;
    }

    public async Task<Result<CashRegisterSessionDto?>> Handle(
        GetOpenCashRegisterSessionQuery request,
        CancellationToken cancellationToken)
    {
        var ensured = await CashRegisterProvisioning.EnsureForWarehouseAsync(
            _warehouses, _registers, request.WarehouseId, _currentUser.UserId?.ToString(), cancellationToken);
        if (ensured.IsFailure)
            return Result.Failure<CashRegisterSessionDto?>(ensured.Error);

        var open = await _sessions.GetOpenByRegisterIdAsync(ensured.Value.Id, cancellationToken);
        if (open is null)
            return Result.Success<CashRegisterSessionDto?>(null);

        return Result.Success<CashRegisterSessionDto?>(await SessionMapper.MapAsync(open, _zReports, cancellationToken));
    }
}

public sealed class OpenCashRegisterSessionCommandHandler
    : IRequestHandler<OpenCashRegisterSessionCommand, Result<CashRegisterSessionDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly ICashRegisterSessionRepository _sessions;
    private readonly IZReportRepository _zReports;
    private readonly IAuditService _auditService;

    public OpenCashRegisterSessionCommandHandler(
        ICurrentUser currentUser,
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        ICashRegisterSessionRepository sessions,
        IZReportRepository zReports,
        IAuditService auditService)
    {
        _currentUser = currentUser;
        _warehouses = warehouses;
        _registers = registers;
        _sessions = sessions;
        _zReports = zReports;
        _auditService = auditService;
    }

    public async Task<Result<CashRegisterSessionDto>> Handle(
        OpenCashRegisterSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            return Result.Failure<CashRegisterSessionDto>(Error.Unauthorized("Utilisateur non identifié"));

        var warehouseId = command.Request.WarehouseId;
        var ensured = await CashRegisterProvisioning.EnsureForWarehouseAsync(
            _warehouses, _registers, warehouseId, userId.ToString(), cancellationToken);
        if (ensured.IsFailure)
            return Result.Failure<CashRegisterSessionDto>(ensured.Error);

        var existing = await _sessions.GetOpenByRegisterIdAsync(ensured.Value.Id, cancellationToken);
        if (existing is not null)
            return Result.Success(await SessionMapper.MapAsync(existing, _zReports, cancellationToken));

        Money floatMoney;
        try
        {
            floatMoney = Money.Create(command.Request.OpeningFloat);
        }
        catch (ArgumentException)
        {
            return Result.Failure<CashRegisterSessionDto>(
                Error.Validation("OpeningFloat", "Le fond de caisse ne peut pas être négatif"));
        }

        var opened = CashRegisterSession.Open(ensured.Value.Id, userId, floatMoney);
        if (opened.IsFailure)
            return Result.Failure<CashRegisterSessionDto>(opened.Error);

        opened.Value.SetAuditInfo(userId.ToString());
        CashRegisterSession saved;
        try
        {
            saved = await _sessions.AddAsync(opened.Value, cancellationToken);
        }
        catch (DbUpdateException)
        {
            var raced = await _sessions.GetOpenByRegisterIdAsync(ensured.Value.Id, cancellationToken);
            if (raced is not null)
                return Result.Success(await SessionMapper.MapAsync(raced, _zReports, cancellationToken));
            throw;
        }

        await _auditService.LogAsync(
            AuditActions.CashRegisterSession.Opened,
            "CashRegisterSession",
            saved.Id,
            newValues: new { saved.CashRegisterId, saved.OpeningFloat.Amount },
            cancellationToken: cancellationToken);

        var reloaded = await _sessions.GetByIdWithRegisterAsync(saved.Id, cancellationToken) ?? saved;
        return Result.Success(await SessionMapper.MapAsync(reloaded, _zReports, cancellationToken));
    }
}

internal static class SessionMapper
{
    public static async Task<CashRegisterSessionDto> MapAsync(
        CashRegisterSession session,
        IZReportRepository zReports,
        CancellationToken cancellationToken)
    {
        string? zNumber = null;
        if (session.ZReportId is { } zId)
        {
            var report = await zReports.GetByIdAsync(zId, cancellationToken);
            zNumber = report?.Number.Value;
        }

        return new CashRegisterSessionDto
        {
            Id = session.Id,
            CashRegisterId = session.CashRegisterId,
            CashRegisterCode = session.CashRegister?.Code ?? string.Empty,
            CashRegisterName = session.CashRegister?.Name ?? string.Empty,
            WarehouseId = session.CashRegister?.WarehouseId ?? Guid.Empty,
            Status = session.Status,
            OpenedAt = session.OpenedAt,
            OpenedByUserId = session.OpenedByUserId,
            OpeningFloat = session.OpeningFloat.Amount,
            ClosedAt = session.ClosedAt,
            ZReportId = session.ZReportId,
            ZReportNumber = zNumber
        };
    }
}
