using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Application.Features.CashRegister;

public sealed record GetPosSessionXReportQuery(Guid WarehouseId) : IRequest<Result<PosSessionReportDto>>;

public sealed record CloseCashRegisterSessionCommand(Guid WarehouseId, CloseCashRegisterSessionRequest Request)
    : IRequest<Result<PosSessionReportDto>>;

public sealed record GetZReportQuery(Guid Id) : IRequest<Result<PosSessionReportDto>>;

public sealed record ListZReportsQuery(DateTime FromUtc, DateTime ToUtc, Guid? CashRegisterId)
    : IRequest<Result<IReadOnlyList<ZReportListItemDto>>>;

internal static class PosSessionTotalsCalculator
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static PosSessionReportDto Build(
        CashRegisterSession session,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Payment> payments,
        IReadOnlyList<CashOperation> cashOperations,
        int heldTicketCount,
        decimal? countedCash = null,
        string? zReportNumber = null,
        string? notes = null)
    {
        var activePayments = payments.Where(p => !p.IsRefunded).ToList();
        var totalsByMethod = activePayments
            .GroupBy(p => p.Method)
            .Select(g =>
            {
                var amount = g.Sum(p => SignedPaymentAmount(p));
                return new PosZTotalsByMethodDto
                {
                    Method = g.Key,
                    MethodDisplay = g.Key.ToDisplayString(),
                    Amount = amount
                };
            })
            .OrderBy(t => t.Method)
            .ToList();

        var cashPayments = activePayments.Where(p => p.Method == PaymentMethod.Cash).Sum(SignedPaymentAmount);
        var manualCash = cashOperations
            .Where(o => o.Status != CashOperationStatus.Annulee
                && o.Origin == CashOperationOrigin.Manual
                && o.Method == PaymentMethod.Cash)
            .Sum(o => o.OperationType == CashOperationType.Credit ? o.Amount.Amount : -o.Amount.Amount);

        var expectedCash = session.OpeningFloat.Amount + cashPayments + manualCash;
        var counted = countedCash ?? session.ClosingCountedCash?.Amount;
        var variance = counted.HasValue ? counted.Value - expectedCash : session.CashVariance?.Amount;

        var liveInvoices = invoices
            .Where(i => i.Status != InvoiceStatus.Cancelled)
            .ToList();

        return new PosSessionReportDto
        {
            SessionId = session.Id,
            CashRegisterId = session.CashRegisterId,
            CashRegisterName = session.CashRegister?.Name ?? string.Empty,
            OpenedAt = session.OpenedAt,
            ClosedAt = session.ClosedAt,
            OpeningFloat = session.OpeningFloat.Amount,
            ExpectedCash = expectedCash,
            CountedCash = counted,
            CashVariance = variance,
            InvoiceCount = liveInvoices.Count(i => i.Type == InvoiceType.Standard),
            CreditNoteCount = liveInvoices.Count(i => i.Type == InvoiceType.CreditNote),
            HeldTicketCount = heldTicketCount,
            TotalsByMethod = totalsByMethod,
            InvoiceIds = liveInvoices.Select(i => i.Id).ToList(),
            ZReportNumber = zReportNumber,
            Notes = notes
        };
    }

    private static decimal SignedPaymentAmount(Payment payment)
    {
        var amount = payment.Amount.Amount;
        return payment.Invoice.IsCreditNote ? -amount : amount;
    }
}

public sealed class GetPosSessionXReportQueryHandler
    : IRequestHandler<GetPosSessionXReportQuery, Result<PosSessionReportDto>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly ICashRegisterSessionRepository _sessions;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentRepository _payments;
    private readonly ICashOperationRepository _cashOperations;
    private readonly IPosHeldTicketRepository _heldTickets;
    private readonly ICurrentUser _currentUser;

    public GetPosSessionXReportQueryHandler(
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        ICashRegisterSessionRepository sessions,
        IInvoiceRepository invoices,
        IPaymentRepository payments,
        ICashOperationRepository cashOperations,
        IPosHeldTicketRepository heldTickets,
        ICurrentUser currentUser)
    {
        _warehouses = warehouses;
        _registers = registers;
        _sessions = sessions;
        _invoices = invoices;
        _payments = payments;
        _cashOperations = cashOperations;
        _heldTickets = heldTickets;
        _currentUser = currentUser;
    }

    public async Task<Result<PosSessionReportDto>> Handle(
        GetPosSessionXReportQuery request,
        CancellationToken cancellationToken)
    {
        var ensured = await CashRegisterProvisioning.EnsureForWarehouseAsync(
            _warehouses, _registers, request.WarehouseId, _currentUser.UserId?.ToString(), cancellationToken);
        if (ensured.IsFailure)
            return Result.Failure<PosSessionReportDto>(ensured.Error);

        var session = await _sessions.GetOpenByRegisterIdAsync(ensured.Value.Id, cancellationToken);
        if (session is null)
            return Result.Failure<PosSessionReportDto>(CashRegisterSessionGuard.Closed);

        return Result.Success(await BuildLiveReportAsync(session, cancellationToken));
    }

    private async Task<PosSessionReportDto> BuildLiveReportAsync(
        CashRegisterSession session,
        CancellationToken cancellationToken,
        decimal? countedCash = null,
        string? zReportNumber = null,
        string? notes = null)
    {
        var invoices = await _invoices.GetByCashRegisterSessionIdAsync(session.Id, cancellationToken);
        var payments = await _payments.GetByCashRegisterSessionIdAsync(session.Id, cancellationToken);
        var cashOps = await _cashOperations.GetByCashRegisterSessionIdAsync(session.Id, cancellationToken);
        var heldCount = await _heldTickets.CountByRegisterAsync(session.CashRegisterId, cancellationToken);
        return PosSessionTotalsCalculator.Build(
            session, invoices, payments, cashOps, heldCount, countedCash, zReportNumber, notes);
    }
}

public sealed class CloseCashRegisterSessionCommandHandler
    : IRequestHandler<CloseCashRegisterSessionCommand, Result<PosSessionReportDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IWarehouseRepository _warehouses;
    private readonly ICashRegisterRepository _registers;
    private readonly ICashRegisterSessionRepository _sessions;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentRepository _payments;
    private readonly ICashOperationRepository _cashOperations;
    private readonly IPosHeldTicketRepository _heldTickets;
    private readonly IZReportRepository _zReports;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public CloseCashRegisterSessionCommandHandler(
        ICurrentUser currentUser,
        IWarehouseRepository warehouses,
        ICashRegisterRepository registers,
        ICashRegisterSessionRepository sessions,
        IInvoiceRepository invoices,
        IPaymentRepository payments,
        ICashOperationRepository cashOperations,
        IPosHeldTicketRepository heldTickets,
        IZReportRepository zReports,
        IDocumentNumberService documentNumbers,
        ITenantUnitOfWork unitOfWork,
        IAuditService auditService)
    {
        _currentUser = currentUser;
        _warehouses = warehouses;
        _registers = registers;
        _sessions = sessions;
        _invoices = invoices;
        _payments = payments;
        _cashOperations = cashOperations;
        _heldTickets = heldTickets;
        _zReports = zReports;
        _documentNumbers = documentNumbers;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Result<PosSessionReportDto>> Handle(
        CloseCashRegisterSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            return Result.Failure<PosSessionReportDto>(Error.Unauthorized("Utilisateur non identifié"));

        PosSessionReportDto? closedReport = null;

        var result = await _unitOfWork.ExecuteAsync(async ct =>
        {
            var ensured = await CashRegisterProvisioning.EnsureForWarehouseAsync(
                _warehouses, _registers, command.WarehouseId, userId.ToString(), ct);
            if (ensured.IsFailure)
                return Result.Failure(ensured.Error);

            var session = await _sessions.GetOpenByRegisterIdAsync(ensured.Value.Id, ct);
            if (session is null)
                return Result.Failure(new Error("POS_SESSION_CLOSED", "Aucune session de caisse ouverte"));

            if (await _zReports.GetBySessionIdAsync(session.Id, ct) is not null)
                return Result.Failure(new Error("POS_SESSION_CLOSED", "Cette session de caisse est déjà clôturée"));

            Money counted;
            try
            {
                counted = Money.Create(command.Request.CountedCash);
            }
            catch (ArgumentException)
            {
                return Result.Failure(Error.Validation("CountedCash", "Le comptage espèces ne peut pas être négatif"));
            }

            var invoices = await _invoices.GetByCashRegisterSessionIdAsync(session.Id, ct);
            var payments = await _payments.GetByCashRegisterSessionIdAsync(session.Id, ct);
            var cashOps = await _cashOperations.GetByCashRegisterSessionIdAsync(session.Id, ct);
            var heldCount = await _heldTickets.CountByRegisterAsync(session.CashRegisterId, ct);

            var preview = PosSessionTotalsCalculator.Build(
                session, invoices, payments, cashOps, heldCount, counted.Amount, notes: command.Request.Notes);

            var tenantId = _currentUser.TenantId ?? Guid.Empty;
            var now = DateTime.UtcNow;
            var reserved = await _documentNumbers.ReserveNextAsync(
                tenantId, NumberingDocumentType.ZReport, now.Year, now, ct);
            var zNumber = ZReportNumber.FromRendered(reserved.Value, reserved.Year, reserved.Sequence);

            var snapshot = preview with { ZReportNumber = zNumber.Value, CountedCash = counted.Amount };
            var snapshotJson = JsonSerializer.Serialize(snapshot, PosSessionTotalsCalculator.JsonOptions);

            var zCreated = ZReport.Create(zNumber, session.Id, snapshotJson);
            if (zCreated.IsFailure)
                return Result.Failure(zCreated.Error);

            var zReport = zCreated.Value;
            zReport.SetAuditInfo(userId.ToString());

            var expected = Money.FromSignedAmount(preview.ExpectedCash, session.OpeningFloat.Currency);
            var closed = session.Close(userId, counted, expected, zReport.Id);
            if (closed.IsFailure)
                return closed;

            session.SetAuditInfo(userId.ToString(), isUpdate: true);

            try
            {
                await _zReports.AddAsync(zReport, ct);
                await _sessions.UpdateAsync(session, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure(CashRegisterSessionGuard.Closed);
            }
            catch (DbUpdateException)
            {
                return Result.Failure(CashRegisterSessionGuard.Closed);
            }

            closedReport = snapshot with { ClosedAt = session.ClosedAt, CashVariance = session.CashVariance?.Amount };
            return Result.Success();
        }, cancellationToken);

        if (result.IsFailure)
            return Result.Failure<PosSessionReportDto>(result.Error);

        await _auditService.LogAsync(
            AuditActions.CashRegisterSession.Closed,
            "CashRegisterSession",
            closedReport!.SessionId,
            newValues: new
            {
                closedReport.ZReportNumber,
                closedReport.ExpectedCash,
                closedReport.CountedCash,
                closedReport.CashVariance
            },
            cancellationToken: cancellationToken);

        return Result.Success(closedReport!);
    }
}

public sealed class GetZReportQueryHandler : IRequestHandler<GetZReportQuery, Result<PosSessionReportDto>>
{
    private readonly IZReportRepository _zReports;

    public GetZReportQueryHandler(IZReportRepository zReports)
    {
        _zReports = zReports;
    }

    public async Task<Result<PosSessionReportDto>> Handle(GetZReportQuery request, CancellationToken cancellationToken)
    {
        var report = await _zReports.GetByIdAsync(request.Id, cancellationToken);
        if (report is null)
            return Result.Failure<PosSessionReportDto>(Error.NotFound("ZReport", request.Id));

        var dto = JsonSerializer.Deserialize<PosSessionReportDto>(
            report.SnapshotJson, PosSessionTotalsCalculator.JsonOptions);
        if (dto is null)
            return Result.Failure<PosSessionReportDto>(
                Error.Validation("SnapshotJson", "Le snapshot de clôture Z est illisible"));

        return Result.Success(dto with { ZReportNumber = report.Number.Value });
    }
}

public sealed class ListZReportsQueryHandler
    : IRequestHandler<ListZReportsQuery, Result<IReadOnlyList<ZReportListItemDto>>>
{
    private readonly IZReportRepository _zReports;
    private readonly ICashRegisterSessionRepository _sessions;

    public ListZReportsQueryHandler(IZReportRepository zReports, ICashRegisterSessionRepository sessions)
    {
        _zReports = zReports;
        _sessions = sessions;
    }

    public async Task<Result<IReadOnlyList<ZReportListItemDto>>> Handle(
        ListZReportsQuery request,
        CancellationToken cancellationToken)
    {
        var reports = await _zReports.ListAsync(request.FromUtc, request.ToUtc, request.CashRegisterId, cancellationToken);
        var items = new List<ZReportListItemDto>(reports.Count);
        foreach (var report in reports)
        {
            var snapshot = JsonSerializer.Deserialize<PosSessionReportDto>(
                report.SnapshotJson, PosSessionTotalsCalculator.JsonOptions);
            var session = await _sessions.GetByIdWithRegisterAsync(report.CashRegisterSessionId, cancellationToken);
            items.Add(new ZReportListItemDto
            {
                Id = report.Id,
                Number = report.Number.Value,
                CashRegisterSessionId = report.CashRegisterSessionId,
                CashRegisterId = snapshot?.CashRegisterId ?? session?.CashRegisterId ?? Guid.Empty,
                CashRegisterName = snapshot?.CashRegisterName ?? session?.CashRegister?.Name ?? string.Empty,
                GeneratedAt = report.GeneratedAt,
                OpeningFloat = snapshot?.OpeningFloat ?? session?.OpeningFloat.Amount ?? 0m,
                ExpectedCash = snapshot?.ExpectedCash ?? session?.ClosingExpectedCash?.Amount ?? 0m,
                CountedCash = snapshot?.CountedCash ?? session?.ClosingCountedCash?.Amount ?? 0m,
                CashVariance = snapshot?.CashVariance ?? session?.CashVariance?.Amount ?? 0m
            });
        }

        return Result.Success<IReadOnlyList<ZReportListItemDto>>(items);
    }
}
