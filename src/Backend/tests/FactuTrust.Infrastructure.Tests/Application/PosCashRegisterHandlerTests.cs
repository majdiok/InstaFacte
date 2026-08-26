using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.CashRegister;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class PosCashRegisterHandlerTests : IDisposable
{
    private readonly string _databaseName;
    private readonly TestTenantDbContextFactory _factory;
    private readonly WarehouseRepository _warehouses;
    private readonly CashRegisterRepository _registers;
    private readonly CashRegisterSessionRepository _sessions;
    private readonly PosCartDraftRepository _carts;
    private readonly PosHeldTicketRepository _held;
    private readonly ZReportRepository _zReports;
    private readonly Guid _userId = Guid.NewGuid();

    public PosCashRegisterHandlerTests()
    {
        _databaseName = $"PosCashRegister_{Guid.NewGuid():N}";
        _factory = new TestTenantDbContextFactory(_databaseName);
        _warehouses = new WarehouseRepository(_factory);
        _registers = new CashRegisterRepository(_factory);
        _sessions = new CashRegisterSessionRepository(_factory);
        _carts = new PosCartDraftRepository(_factory);
        _held = new PosHeldTicketRepository(_factory);
        _zReports = new ZReportRepository(_factory);
    }

    public void Dispose()
    {
        using var context = _factory.CreateContext();
        context.Database.EnsureDeleted();
    }

    [Fact]
    public async Task SaveGetClearCart_RoundTripsJsonForUserAndRegister()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        using var doc = JsonDocument.Parse("""{"ticketId":"abc","lines":[{"q":1}]}""");

        var save = await new SavePosCartDraftCommandHandler(currentUser, _warehouses, _registers, _carts)
            .Handle(new SavePosCartDraftCommand(warehouse.Id, doc.RootElement.Clone()), CancellationToken.None);
        Assert.True(save.IsSuccess, save.Error?.Description);

        var get = await new GetPosCartDraftQueryHandler(currentUser, _warehouses, _registers, _carts)
            .Handle(new GetPosCartDraftQuery(warehouse.Id), CancellationToken.None);
        Assert.True(get.IsSuccess);
        Assert.NotNull(get.Value.State);
        Assert.Contains("ticketId", JsonSerializer.Serialize(get.Value.State));

        var clear = await new ClearPosCartDraftCommandHandler(currentUser, _registers, _carts)
            .Handle(new ClearPosCartDraftCommand(warehouse.Id), CancellationToken.None);
        Assert.True(clear.IsSuccess);

        var empty = await new GetPosCartDraftQueryHandler(currentUser, _warehouses, _registers, _carts)
            .Handle(new GetPosCartDraftQuery(warehouse.Id), CancellationToken.None);
        Assert.True(empty.IsSuccess);
        Assert.Null(empty.Value.State);
    }

    [Fact]
    public async Task SaveCart_WithoutUser_ReturnsUnauthorized()
    {
        var currentUser = new Mock<ICurrentUser>();
        using var doc = JsonDocument.Parse("""{"lines":[1]}""");
        var result = await new SavePosCartDraftCommandHandler(
                currentUser.Object, _warehouses, _registers, _carts)
            .Handle(new SavePosCartDraftCommand(Guid.NewGuid(), doc.RootElement.Clone()), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task HeldTicket_SaveListRecall_DeletesOnRecall()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        using var doc = JsonDocument.Parse("""{"lines":[{"designation":"Pain"}]}""");
        var request = new SavePosHeldTicketRequest
        {
            WarehouseId = warehouse.Id,
            Label = "Client passager - 1 article",
            TotalTtc = 12.5m,
            LineCount = 1,
            State = doc.RootElement.Clone()
        };

        var saved = await new SavePosHeldTicketCommandHandler(
                currentUser, _warehouses, _registers, _sessions, _held)
            .Handle(new SavePosHeldTicketCommand(request), CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Description);

        var list = await new ListPosHeldTicketsQueryHandler(_warehouses, _registers, _held, currentUser)
            .Handle(new ListPosHeldTicketsQuery(warehouse.Id), CancellationToken.None);
        Assert.True(list.IsSuccess);
        Assert.Single(list.Value);

        var recalled = await new RecallPosHeldTicketCommandHandler(_held)
            .Handle(new RecallPosHeldTicketCommand(saved.Value.Id), CancellationToken.None);
        Assert.True(recalled.IsSuccess);
        Assert.Equal("Client passager - 1 article", recalled.Value.Label);

        var after = await new ListPosHeldTicketsQueryHandler(_warehouses, _registers, _held, currentUser)
            .Handle(new ListPosHeldTicketsQuery(warehouse.Id), CancellationToken.None);
        Assert.Empty(after.Value);
    }

    [Fact]
    public async Task ImportHeldTickets_UsesMediatorToSaveEach()
    {
        var warehouse = await SeedWarehouseAsync();
        using var doc = JsonDocument.Parse("""{"lines":[1]}""");
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<SavePosHeldTicketCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PosHeldTicketDto { Id = Guid.NewGuid(), Label = "x" }));

        var imported = await new ImportPosHeldTicketsCommandHandler(mediator.Object)
            .Handle(new ImportPosHeldTicketsCommand(new ImportPosHeldTicketsRequest
            {
                WarehouseId = warehouse.Id,
                Tickets =
                [
                    new SavePosHeldTicketRequest
                    {
                        Label = "A",
                        TotalTtc = 1,
                        LineCount = 1,
                        State = doc.RootElement.Clone()
                    }
                ]
            }), CancellationToken.None);

        Assert.True(imported.IsSuccess);
        Assert.Equal(1, imported.Value);
        mediator.Verify(m => m.Send(It.IsAny<SavePosHeldTicketCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenSession_SecondCall_IsIdempotentAndDoesNotChangeFloat()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        var audit = new Mock<IAuditService>();
        var handler = new OpenCashRegisterSessionCommandHandler(
            currentUser, _warehouses, _registers, _sessions, _zReports, audit.Object);

        var first = await handler.Handle(
            new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
            {
                WarehouseId = warehouse.Id,
                OpeningFloat = 50m
            }), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Description);

        var second = await handler.Handle(
            new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
            {
                WarehouseId = warehouse.Id,
                OpeningFloat = 999m
            }), CancellationToken.None);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal(50m, second.Value.OpeningFloat);
        audit.Verify(
            a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCashRegister_SetsRequireOpenSessionFromFlag()
    {
        var warehouse = await SeedWarehouseAsync();
        var handler = new GetCashRegisterQueryHandler(
            _warehouses,
            _registers,
            CurrentUser(),
            Options.Create(new CashDeskFeaturesOptions { PosRegisterSessions = false }));

        var result = await handler.Handle(new GetCashRegisterQuery(warehouse.Id), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.RequireOpenSession);
        Assert.StartsWith("WH-", result.Value.Code);
    }

    [Fact]
    public void Totals_ExpectedCash_IgnoresAutoInvoicePaymentCashOps()
    {
        var register = CashRegister.Create("WH-MAIN", "Caisse Principal", Guid.NewGuid()).Value;
        var session = CashRegisterSession.Open(register.Id, _userId, Money.Create(100m)).Value;

        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("c@example.com").Value;
        var client = Client.Create("Client", ClientType.Individual, address, email).Value;
        var fac = Invoice.Create(InvoiceNumber.Create("FAC", 2026, 1), client, new DateTime(2026, 8, 18)).Value;
        fac.AssignCashRegisterSession(session.Id);
        var avo = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", 2026, 1), client, new DateTime(2026, 8, 18), fac.Id).Value;
        avo.AssignCashRegisterSession(session.Id);

        var cashIn = Payment.Create(fac, Money.Create(50m), DateTime.UtcNow, PaymentMethod.Cash).Value;
        cashIn.AssignCashRegisterSession(session.Id);
        var card = Payment.Create(fac, Money.Create(30m), DateTime.UtcNow, PaymentMethod.Card).Value;
        card.AssignCashRegisterSession(session.Id);
        var refund = Payment.Create(avo, Money.Create(10m), DateTime.UtcNow, PaymentMethod.Cash).Value;
        refund.AssignCashRegisterSession(session.Id);

        var autoCash = CashOperation.CreateFromInvoicePayment(
            CashOperationNumber.Create("ENC", 2026, 1).Value,
            cashIn.Id,
            fac.Number.Value,
            Money.Create(50m),
            DateTime.UtcNow).Value;
        autoCash.AssignCashRegisterSession(session.Id);

        var manualIn = CashOperation.Create(
            CashOperationNumber.Create("ENC", 2026, 2).Value,
            CashOperationType.Credit,
            DateTime.UtcNow,
            PaymentMethod.Cash,
            Money.Create(20m),
            "Fond complementaire",
            revenueCategory: CashRevenueCategory.Other).Value;
        manualIn.AssignCashRegisterSession(session.Id);

        var manualOut = CashOperation.Create(
            CashOperationNumber.Create("DEP", 2026, 1).Value,
            CashOperationType.Debit,
            DateTime.UtcNow,
            PaymentMethod.Cash,
            Money.Create(5m),
            "Monnaie rendue",
            category: CashExpenseCategory.Other).Value;
        manualOut.AssignCashRegisterSession(session.Id);

        var report = PosSessionTotalsCalculator.Build(
            session,
            [fac, avo],
            [cashIn, card, refund],
            [autoCash, manualIn, manualOut],
            heldTicketCount: 2);

        Assert.Equal(155m, report.ExpectedCash);
        Assert.Equal(1, report.InvoiceCount);
        Assert.Equal(1, report.CreditNoteCount);
        Assert.Equal(2, report.HeldTicketCount);
        Assert.Equal(40m, report.TotalsByMethod.Single(t => t.Method == PaymentMethod.Cash).Amount);
        Assert.Equal(30m, report.TotalsByMethod.Single(t => t.Method == PaymentMethod.Card).Amount);
    }

    [Fact]
    public void Totals_ExpectedCash_IgnoresBankTransferAndCard()
    {
        var register = CashRegister.Create("WH-MAIN", "Caisse Principal", Guid.NewGuid()).Value;
        var session = CashRegisterSession.Open(register.Id, _userId, Money.Create(40m)).Value;
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("c@example.com").Value;
        var client = Client.Create("Client", ClientType.Individual, address, email).Value;
        var fac = Invoice.Create(InvoiceNumber.Create("FAC", 2026, 2), client, new DateTime(2026, 8, 18)).Value;
        fac.AssignCashRegisterSession(session.Id);

        var cash = Payment.Create(fac, Money.Create(15m), DateTime.UtcNow, PaymentMethod.Cash).Value;
        cash.AssignCashRegisterSession(session.Id);
        var transfer = Payment.Create(fac, Money.Create(80m), DateTime.UtcNow, PaymentMethod.BankTransfer).Value;
        transfer.AssignCashRegisterSession(session.Id);
        var card = Payment.Create(fac, Money.Create(25m), DateTime.UtcNow, PaymentMethod.Card).Value;
        card.AssignCashRegisterSession(session.Id);

        var report = PosSessionTotalsCalculator.Build(session, [fac], [cash, transfer, card], [], heldTicketCount: 0);

        Assert.Equal(55m, report.ExpectedCash);
        Assert.Equal(80m, report.TotalsByMethod.Single(t => t.Method == PaymentMethod.BankTransfer).Amount);
        Assert.Equal(25m, report.TotalsByMethod.Single(t => t.Method == PaymentMethod.Card).Amount);
    }

    [Fact]
    public async Task GetByWarehouseId_AfterProvisioning_ReturnsSingleRegister()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        var first = await new GetCashRegisterQueryHandler(
                _warehouses,
                _registers,
                currentUser,
                Options.Create(new CashDeskFeaturesOptions { PosRegisterSessions = true }))
            .Handle(new GetCashRegisterQuery(warehouse.Id), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Description);

        var again = await _registers.GetByWarehouseIdAsync(warehouse.Id);
        Assert.NotNull(again);
        Assert.Equal(first.Value.Id, again!.Id);

        var all = await _registers.GetAllAsync();
        Assert.Single(all.Where(r => r.WarehouseId == warehouse.Id));
    }

    [Fact]
    public async Task CloseSession_PersistsImmutableSnapshot_AndSecondCloseFails()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        var opened = await new OpenCashRegisterSessionCommandHandler(
                currentUser, _warehouses, _registers, _sessions, _zReports, new Mock<IAuditService>().Object)
            .Handle(new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
            {
                WarehouseId = warehouse.Id,
                OpeningFloat = 10m
            }), CancellationToken.None);
        Assert.True(opened.IsSuccess, opened.Error?.Description);

        var numbers = new Mock<IDocumentNumberService>();
        numbers.Setup(n => n.ReserveNextAsync(
                It.IsAny<Guid>(), NumberingDocumentType.ZReport, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentNumberResult("Z-2026-000001", 2026, 1, "Z"));

        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(i => i.GetByCashRegisterSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Invoice>());
        var payments = new Mock<IPaymentRepository>();
        payments.Setup(p => p.GetByCashRegisterSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Payment>());
        var cashOps = new Mock<ICashOperationRepository>();
        cashOps.Setup(c => c.GetByCashRegisterSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CashOperation>());

        var stillOpen = await _sessions.GetOpenByRegisterIdAsync(opened.Value.CashRegisterId, CancellationToken.None);
        Assert.NotNull(stillOpen);

        var closer = new CloseCashRegisterSessionCommandHandler(
            currentUser,
            _warehouses,
            _registers,
            _sessions,
            invoices.Object,
            payments.Object,
            cashOps.Object,
            _held,
            _zReports,
            numbers.Object,
            new PassthroughTenantUnitOfWork(),
            new Mock<IAuditService>().Object);

        var closed = await closer.Handle(
            new CloseCashRegisterSessionCommand(warehouse.Id, new CloseCashRegisterSessionRequest { CountedCash = 12m }),
            CancellationToken.None);
        Assert.True(closed.IsSuccess, $"{closed.Error?.Code}: {closed.Error?.Description}");
        Assert.Equal("Z-2026-000001", closed.Value.ZReportNumber);
        Assert.Equal(10m, closed.Value.ExpectedCash);
        Assert.Equal(2m, closed.Value.CashVariance);

        var again = await closer.Handle(
            new CloseCashRegisterSessionCommand(warehouse.Id, new CloseCashRegisterSessionRequest { CountedCash = 12m }),
            CancellationToken.None);
        Assert.True(again.IsFailure);
        Assert.Equal("POS_SESSION_CLOSED", again.Error.Code);
    }

    [Fact]
    public async Task TwoRegisters_CanHaveTwoOpenSessions()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        var features = Options.Create(new CashDeskFeaturesOptions { PosRegisterSessions = true });

        var first = await new GetCashRegisterQueryHandler(_warehouses, _registers, currentUser, features)
            .Handle(new GetCashRegisterQuery(warehouse.Id), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await new CreateCashRegisterCommandHandler(_warehouses, _registers, currentUser, features)
            .Handle(new CreateCashRegisterCommand(new CreateCashRegisterRequest
            {
                WarehouseId = warehouse.Id,
                Code = "CAISSE-2",
                Name = "Caisse 2",
                IsDefault = false
            }), CancellationToken.None);
        Assert.True(second.IsSuccess, second.Error?.Description);
        Assert.False(second.Value.IsDefault);
        Assert.True(first.Value.IsDefault);

        var opener = new OpenCashRegisterSessionCommandHandler(
            currentUser, _warehouses, _registers, _sessions, _zReports, new Mock<IAuditService>().Object);

        var s1 = await opener.Handle(new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
        {
            WarehouseId = warehouse.Id,
            OpeningFloat = 10m
        }), CancellationToken.None);
        var s2 = await opener.Handle(new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
        {
            WarehouseId = warehouse.Id,
            CashRegisterId = second.Value.Id,
            OpeningFloat = 20m
        }), CancellationToken.None);

        Assert.True(s1.IsSuccess, s1.Error?.Description);
        Assert.True(s2.IsSuccess, s2.Error?.Description);
        Assert.NotEqual(s1.Value.Id, s2.Value.Id);
        Assert.Equal(first.Value.Id, s1.Value.CashRegisterId);
        Assert.Equal(second.Value.Id, s2.Value.CashRegisterId);
        Assert.Equal(10m, s1.Value.OpeningFloat);
        Assert.Equal(20m, s2.Value.OpeningFloat);
    }

    [Fact]
    public async Task OpenWithoutCashRegisterId_UsesDefault_AndDoesNotChangeFloat()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        var opener = new OpenCashRegisterSessionCommandHandler(
            currentUser, _warehouses, _registers, _sessions, _zReports, new Mock<IAuditService>().Object);

        var first = await opener.Handle(new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
        {
            WarehouseId = warehouse.Id,
            OpeningFloat = 15m
        }), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await opener.Handle(new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
        {
            WarehouseId = warehouse.Id,
            OpeningFloat = 99m
        }), CancellationToken.None);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal(15m, second.Value.OpeningFloat);
        Assert.True((await _registers.GetDefaultByWarehouseIdAsync(warehouse.Id))!.IsDefault);
    }

    [Fact]
    public async Task SaveCart_TwoRapidSaves_LastWriteWins()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        using var cartFirst = JsonDocument.Parse("""{"ticketId":"first","lines":[{"q":1}]}""");
        using var cartSecond = JsonDocument.Parse("""{"ticketId":"second","lines":[{"q":2}]}""");

        var saver = new SavePosCartDraftCommandHandler(currentUser, _warehouses, _registers, _carts);
        var first = await saver.Handle(
            new SavePosCartDraftCommand(warehouse.Id, cartFirst.RootElement.Clone()), CancellationToken.None);
        var second = await saver.Handle(
            new SavePosCartDraftCommand(warehouse.Id, cartSecond.RootElement.Clone()), CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error?.Description);
        Assert.True(second.IsSuccess, second.Error?.Description);

        var get = await new GetPosCartDraftQueryHandler(currentUser, _warehouses, _registers, _carts)
            .Handle(new GetPosCartDraftQuery(warehouse.Id), CancellationToken.None);
        Assert.True(get.IsSuccess);
        var json = JsonSerializer.Serialize(get.Value.State);
        Assert.Contains("second", json);
        Assert.DoesNotContain("first", json);
    }

    [Fact]
    public async Task Cart_IsIsolatedPerRegister()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        var features = Options.Create(new CashDeskFeaturesOptions { PosRegisterSessions = true });
        var defaultRegister = await new GetCashRegisterQueryHandler(_warehouses, _registers, currentUser, features)
            .Handle(new GetCashRegisterQuery(warehouse.Id), CancellationToken.None);
        var other = await new CreateCashRegisterCommandHandler(_warehouses, _registers, currentUser, features)
            .Handle(new CreateCashRegisterCommand(new CreateCashRegisterRequest
            {
                WarehouseId = warehouse.Id,
                Code = "CAISSE-B",
                Name = "Caisse B",
                IsDefault = false
            }), CancellationToken.None);
        Assert.True(other.IsSuccess, other.Error?.Description);

        using var cartA = JsonDocument.Parse("""{"ticket":"A"}""");
        using var cartB = JsonDocument.Parse("""{"ticket":"B"}""");

        var saver = new SavePosCartDraftCommandHandler(currentUser, _warehouses, _registers, _carts);
        Assert.True((await saver.Handle(
            new SavePosCartDraftCommand(warehouse.Id, cartA.RootElement.Clone()), CancellationToken.None)).IsSuccess);
        Assert.True((await saver.Handle(
            new SavePosCartDraftCommand(warehouse.Id, cartB.RootElement.Clone(), other.Value.Id), CancellationToken.None)).IsSuccess);

        var getter = new GetPosCartDraftQueryHandler(currentUser, _warehouses, _registers, _carts);
        var a = await getter.Handle(new GetPosCartDraftQuery(warehouse.Id), CancellationToken.None);
        var b = await getter.Handle(new GetPosCartDraftQuery(warehouse.Id, other.Value.Id), CancellationToken.None);

        Assert.Contains("A", JsonSerializer.Serialize(a.Value.State));
        Assert.Contains("B", JsonSerializer.Serialize(b.Value.State));
        Assert.Equal(defaultRegister.Value.Id, (await _registers.GetDefaultByWarehouseIdAsync(warehouse.Id))!.Id);
    }

    [Fact]
    public async Task XReport_DoesNotIncludeInvoicesFromOtherRegister()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        var features = Options.Create(new CashDeskFeaturesOptions { PosRegisterSessions = true });
        var first = await new GetCashRegisterQueryHandler(_warehouses, _registers, currentUser, features)
            .Handle(new GetCashRegisterQuery(warehouse.Id), CancellationToken.None);
        Assert.True(first.IsSuccess);
        var second = await new CreateCashRegisterCommandHandler(_warehouses, _registers, currentUser, features)
            .Handle(new CreateCashRegisterCommand(new CreateCashRegisterRequest
            {
                WarehouseId = warehouse.Id,
                Code = "CAISSE-X",
                Name = "Caisse X",
                IsDefault = false
            }), CancellationToken.None);
        Assert.True(second.IsSuccess, second.Error?.Description);

        var opener = new OpenCashRegisterSessionCommandHandler(
            currentUser, _warehouses, _registers, _sessions, _zReports, new Mock<IAuditService>().Object);
        var s1 = await opener.Handle(new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
        {
            WarehouseId = warehouse.Id,
            OpeningFloat = 0m
        }), CancellationToken.None);
        var s2 = await opener.Handle(new OpenCashRegisterSessionCommand(new OpenCashRegisterSessionRequest
        {
            WarehouseId = warehouse.Id,
            CashRegisterId = second.Value.Id,
            OpeningFloat = 0m
        }), CancellationToken.None);
        Assert.True(s1.IsSuccess && s2.IsSuccess);
        Assert.NotEqual(s1.Value.Id, s2.Value.Id);
        Assert.Equal(first.Value.Id, s1.Value.CashRegisterId);

        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("z@example.com").Value;
        var client = Client.Create("Client", ClientType.Individual, address, email).Value;
        var fac = Invoice.Create(InvoiceNumber.Create("FAC", 2026, 9), client, new DateTime(2026, 8, 18)).Value;
        fac.AssignCashRegisterSession(s2.Value.Id);

        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(i => i.GetByCashRegisterSessionIdAsync(s1.Value.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Invoice>());
        invoices.Setup(i => i.GetByCashRegisterSessionIdAsync(s2.Value.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fac });
        var payments = new Mock<IPaymentRepository>();
        payments.Setup(p => p.GetByCashRegisterSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Payment>());
        var cashOps = new Mock<ICashOperationRepository>();
        cashOps.Setup(c => c.GetByCashRegisterSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CashOperation>());

        var xHandler = new GetPosSessionXReportQueryHandler(
            _warehouses, _registers, _sessions, invoices.Object, payments.Object, cashOps.Object, _held, currentUser);

        var x1 = await xHandler.Handle(new GetPosSessionXReportQuery(warehouse.Id), CancellationToken.None);
        var x2 = await xHandler.Handle(
            new GetPosSessionXReportQuery(warehouse.Id, second.Value.Id), CancellationToken.None);

        Assert.True(x1.IsSuccess, x1.Error?.Description);
        Assert.True(x2.IsSuccess, x2.Error?.Description);
        Assert.Equal(0, x1.Value.InvoiceCount);
        Assert.Equal(1, x2.Value.InvoiceCount);
    }

    [Fact]
    public async Task Provisioning_SecondCall_DoesNotInsertSecondDefault()
    {
        var warehouse = await SeedWarehouseAsync();
        var currentUser = CurrentUser();
        var features = Options.Create(new CashDeskFeaturesOptions { PosRegisterSessions = true });
        var handler = new GetCashRegisterQueryHandler(_warehouses, _registers, currentUser, features);

        var first = await handler.Handle(new GetCashRegisterQuery(warehouse.Id), CancellationToken.None);
        var second = await handler.Handle(new GetCashRegisterQuery(warehouse.Id), CancellationToken.None);
        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);

        var listed = await new ListCashRegistersQueryHandler(_warehouses, _registers, currentUser, features)
            .Handle(new ListCashRegistersQuery(warehouse.Id), CancellationToken.None);
        Assert.True(listed.IsSuccess);
        Assert.Single(listed.Value);
        Assert.True(listed.Value[0].IsDefault);
    }

    private sealed class PassthroughTenantUnitOfWork : ITenantUnitOfWork
    {
        public Task<Result> ExecuteAsync(
            Func<CancellationToken, Task<Result>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task<Result<T>> ExecuteAsync<T>(
            Func<CancellationToken, Task<Result<T>>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);
    }

    private ICurrentUser CurrentUser()
    {
        var mock = new Mock<ICurrentUser>();
        mock.SetupGet(x => x.UserId).Returns(_userId);
        mock.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        return mock.Object;
    }

    private async Task<Warehouse> SeedWarehouseAsync()
    {
        var warehouse = Warehouse.Create("MAIN", "Principal", isDefault: true).Value;
        return await _warehouses.AddAsync(warehouse);
    }

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;

        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }
}
