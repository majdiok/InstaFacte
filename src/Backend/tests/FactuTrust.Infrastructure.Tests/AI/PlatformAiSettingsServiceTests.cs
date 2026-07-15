using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class PlatformAiSettingsServiceTests
{
    private static PlatformAiSettingsService CreateService(MasterDbContext db, IMemoryCache? cache = null) =>
        new(db, cache ?? new MemoryCache(new MemoryCacheOptions()), NullLogger<PlatformAiSettingsService>.Instance);

    [Fact]
    public async Task GetInvoiceImportModelRefAsync_WhenNoRow_ReturnsNull()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);

        var result = await service.GetInvoiceImportModelRefAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetInvoiceImportModelRefAsync_WhenConfigured_ReturnsNormalizedRef()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var row = PlatformAiSettings.CreateDefaults();
        row.SetInvoiceImportModel("qwen2.5:7b-instruct");
        db.PlatformAiSettings.Add(row);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.GetInvoiceImportModelRefAsync();

        Assert.Equal("qwen2.5:7b-instruct", result);
    }

    [Fact]
    public async Task GetInferenceDeviceAsync_WhenNoRow_ReturnsGpu()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);

        var result = await service.GetInferenceDeviceAsync();

        Assert.Equal(OllamaInferenceDevice.Gpu, result);
    }

    [Fact]
    public async Task SetInferenceDeviceAsync_PersistsCpuOnly()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);
        var actorId = Guid.NewGuid();

        var stored = await service.SetInferenceDeviceAsync(OllamaInferenceDevice.CpuOnly, actorId);
        var read = await service.GetInferenceDeviceAsync();

        Assert.Equal(OllamaInferenceDevice.CpuOnly, stored);
        Assert.Equal(OllamaInferenceDevice.CpuOnly, read);
    }

    [Fact]
    public async Task GetInferenceDeviceAsync_UsesCache_AfterFirstRead()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var row = PlatformAiSettings.CreateDefaults();
        row.SetInferenceDevice(OllamaInferenceDevice.CpuOnly);
        db.PlatformAiSettings.Add(row);
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache);

        var first = await service.GetInferenceDeviceAsync();
        db.PlatformAiSettings.Remove(row);
        await db.SaveChangesAsync();

        var second = await service.GetInferenceDeviceAsync();

        Assert.Equal(OllamaInferenceDevice.CpuOnly, first);
        Assert.Equal(OllamaInferenceDevice.CpuOnly, second);
    }
}
