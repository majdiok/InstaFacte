using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Reports.Queries;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GetStockSnapshotAtDateQueryHandlerTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static GetStockSnapshotAtDateQueryHandler CreateHandler(
        TimeProvider timeProvider,
        out Mock<IStockMovementRepository> repository)
    {
        repository = new Mock<IStockMovementRepository>();
        repository
            .Setup(x => x.GetStockSnapshotAtDateAsync(
                It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockSnapshotRowDto>());
        return new GetStockSnapshotAtDateQueryHandler(repository.Object, timeProvider);
    }

    [Fact]
    public async Task Handle_AsOfTunisiaToday_WhenUtcStillPreviousDay_IsAccepted()
    {
        // 23:30 UTC le 02/06 = 00:30 le 03/06 en Afrique/Tunis (UTC+1) → "aujourd'hui" = 03/06.
        // Avant le correctif, DateTime.UtcNow.Date == 02/06 rejetait à tort le 03/06 comme date future.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 2, 23, 30, 0, TimeSpan.Zero));
        var handler = CreateHandler(clock, out var repository);

        var result = await handler.Handle(
            new GetStockSnapshotAtDateQuery(new DateTime(2026, 6, 3), WarehouseId: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        repository.Verify(
            x => x.GetStockSnapshotAtDateAsync(
                new DateTime(2026, 6, 3).AddDays(1).AddTicks(-1),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AsOfBeyondTunisiaToday_IsRejected()
    {
        // Même instant (today Tunisie = 03/06) : le 04/06 reste une date future et doit être rejeté.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 2, 23, 30, 0, TimeSpan.Zero));
        var handler = CreateHandler(clock, out var repository);

        var result = await handler.Handle(
            new GetStockSnapshotAtDateQuery(new DateTime(2026, 6, 4), WarehouseId: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.AsOfDate", result.Error.Code);
        repository.Verify(
            x => x.GetStockSnapshotAtDateAsync(
                It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AsOfPastDate_IsAccepted()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
        var handler = CreateHandler(clock, out var repository);

        var result = await handler.Handle(
            new GetStockSnapshotAtDateQuery(new DateTime(2026, 5, 1), WarehouseId: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        repository.Verify(
            x => x.GetStockSnapshotAtDateAsync(
                new DateTime(2026, 5, 1).AddDays(1).AddTicks(-1),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
