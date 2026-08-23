using System;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class CashRegisterTests
{
    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        var warehouseId = Guid.NewGuid();
        var result = CashRegister.Create("WH-MAIN", "Caisse principal", warehouseId);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal("WH-MAIN", result.Value.Code);
        Assert.Equal("Caisse principal", result.Value.Name);
        Assert.Equal(warehouseId, result.Value.WarehouseId);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_NormalizesCodeToUpper()
    {
        var result = CashRegister.Create("wh-a", "Caisse A", Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal("WH-A", result.Value.Code);
    }

    [Fact]
    public void Create_WithEmptyWarehouse_Fails()
    {
        var result = CashRegister.Create("WH-A", "Caisse", Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.WarehouseId", result.Error.Code);
    }

    [Fact]
    public void Create_WithCodeTooLong_Fails()
    {
        var result = CashRegister.Create(new string('A', 21), "Caisse", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Code", result.Error.Code);
    }

    [Fact]
    public void DefaultCodeForWarehouse_TruncatesTo20()
    {
        var code = CashRegister.DefaultCodeForWarehouse("WAREHOUSETOOLONGNAME");

        Assert.Equal(20, code.Length);
        Assert.StartsWith("WH-", code, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultCodeForWarehouse_ShortCode_KeepsPrefix()
    {
        Assert.Equal("WH-PRIN", CashRegister.DefaultCodeForWarehouse("prin"));
    }
}

public sealed class CashRegisterSessionTests
{
    [Fact]
    public void Open_WithValidFloat_Succeeds()
    {
        var registerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var result = CashRegisterSession.Open(registerId, userId, Money.Create(50m));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(CashRegisterSessionStatus.Open, result.Value.Status);
        Assert.Equal(50m, result.Value.OpeningFloat.Amount);
        Assert.Equal(registerId, result.Value.CashRegisterId);
        Assert.Equal(userId, result.Value.OpenedByUserId);
        Assert.Null(result.Value.ClosedAt);
        Assert.Null(result.Value.ZReportId);
    }

    [Fact]
    public void Open_WithZeroFloat_Succeeds()
    {
        var result = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Zero());

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(0m, result.Value.OpeningFloat.Amount);
    }

    [Fact]
    public void Open_WithEmptyRegister_Fails()
    {
        var result = CashRegisterSession.Open(Guid.Empty, Guid.NewGuid(), Money.Zero());

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.CashRegisterId", result.Error.Code);
    }

    [Fact]
    public void Open_WithEmptyUser_Fails()
    {
        var result = CashRegisterSession.Open(Guid.NewGuid(), Guid.Empty, Money.Zero());

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.OpenedByUserId", result.Error.Code);
    }

    [Fact]
    public void Close_ComputesVarianceCountedMinusExpected()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(100m)).Value;
        var zReportId = Guid.NewGuid();

        var close = session.Close(
            Guid.NewGuid(),
            countedCash: Money.Create(95m),
            expectedCash: Money.Create(100m),
            zReportId);

        Assert.True(close.IsSuccess, close.Error?.Description);
        Assert.Equal(CashRegisterSessionStatus.Closed, session.Status);
        Assert.Equal(zReportId, session.ZReportId);
        Assert.NotNull(session.ClosedAt);
        Assert.Equal(95m, session.ClosingCountedCash!.Amount);
        Assert.Equal(100m, session.ClosingExpectedCash!.Amount);
        Assert.Equal(-5m, session.CashVariance!.Amount);
    }

    [Fact]
    public void Close_PositiveVariance_WhenCountedAboveExpected()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(10m)).Value;

        var close = session.Close(Guid.NewGuid(), Money.Create(12.5m), Money.Create(10m), Guid.NewGuid());

        Assert.True(close.IsSuccess);
        Assert.Equal(2.5m, session.CashVariance!.Amount);
    }

    [Fact]
    public void Close_WhenAlreadyClosed_Fails()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Zero()).Value;
        Assert.True(session.Close(Guid.NewGuid(), Money.Zero(), Money.Zero(), Guid.NewGuid()).IsSuccess);

        var second = session.Close(Guid.NewGuid(), Money.Zero(), Money.Zero(), Guid.NewGuid());

        Assert.True(second.IsFailure);
        Assert.Equal("Validation.Status", second.Error.Code);
    }

    [Fact]
    public void Close_WithEmptyZReportId_Fails()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Zero()).Value;

        var close = session.Close(Guid.NewGuid(), Money.Zero(), Money.Zero(), Guid.Empty);

        Assert.True(close.IsFailure);
        Assert.Equal("Validation.ZReportId", close.Error.Code);
        Assert.Equal(CashRegisterSessionStatus.Open, session.Status);
    }

    [Fact]
    public void Close_WithNegativeCounted_Fails()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Zero()).Value;

        var close = session.Close(
            Guid.NewGuid(),
            Money.FromSignedAmount(-1m),
            Money.Zero(),
            Guid.NewGuid());

        Assert.True(close.IsFailure);
        Assert.Equal("Validation.CountedCash", close.Error.Code);
        Assert.Equal(CashRegisterSessionStatus.Open, session.Status);
    }

    [Fact]
    public void Close_UsesDistinctMoneyInstances()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(1m)).Value;
        var counted = Money.Create(1m);
        var expected = Money.Create(1m);

        var close = session.Close(Guid.NewGuid(), counted, expected, Guid.NewGuid());

        Assert.True(close.IsSuccess);
        Assert.NotSame(counted, session.ClosingCountedCash);
        Assert.NotSame(expected, session.ClosingExpectedCash);
        Assert.NotSame(session.ClosingCountedCash, session.CashVariance);
    }
}

public sealed class ZReportTests
{
    [Fact]
    public void Create_WithValidSnapshot_Succeeds()
    {
        var number = ZReportNumber.Generate(2026, 1).Value;
        var sessionId = Guid.NewGuid();

        var result = ZReport.Create(number, sessionId, """{"openingFloat":0}""");

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal("Z-2026-000001", result.Value.Number.Value);
        Assert.Equal(sessionId, result.Value.CashRegisterSessionId);
        Assert.Contains("openingFloat", result.Value.SnapshotJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WithEmptySnapshot_Fails()
    {
        var result = ZReport.Create(ZReportNumber.Generate(2026, 1).Value, Guid.NewGuid(), "  ");

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.SnapshotJson", result.Error.Code);
    }
}

public sealed class PosCartDraftTests
{
    [Fact]
    public void CreateAndReplaceState_RoundTripsJson()
    {
        var userId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var created = PosCartDraft.Create(userId, registerId, """{"lines":[1]}""");

        Assert.True(created.IsSuccess);
        Assert.Equal(userId, created.Value.UserId);
        Assert.Equal(registerId, created.Value.CashRegisterId);

        var replaced = created.Value.ReplaceState("""{"lines":[1,2]}""");
        Assert.True(replaced.IsSuccess);
        Assert.Equal("""{"lines":[1,2]}""", created.Value.StateJson);
    }

    [Fact]
    public void Create_WithEmptyJson_Fails()
    {
        var result = PosCartDraft.Create(Guid.NewGuid(), Guid.NewGuid(), " ");

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.StateJson", result.Error.Code);
    }
}

public sealed class PosHeldTicketTests
{
    [Fact]
    public void Create_WithValidTicket_Succeeds()
    {
        var result = PosHeldTicket.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Client passager - 2 articles",
            12.500m,
            2,
            """{"lines":[{}]}""",
            Guid.NewGuid());

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(12.500m, result.Value.TotalTtc);
        Assert.Equal(2, result.Value.LineCount);
    }

    [Fact]
    public void Create_WithZeroLines_Fails()
    {
        var result = PosHeldTicket.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Vide",
            0,
            0,
            """{"lines":[]}""");

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.LineCount", result.Error.Code);
    }
}
