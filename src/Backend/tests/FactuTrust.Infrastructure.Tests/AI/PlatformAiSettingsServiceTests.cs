using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class PlatformAiSettingsServiceTests
{
    private static PlatformAiSettingsService CreateService(MasterDbContext db, IMemoryCache? cache = null)
    {
        var protection = new EphemeralDataProtectionProvider();
        var openRouter = Options.Create(new OpenRouterSettings
        {
            DefaultBaseUrl = "https://openrouter.ai/api/v1"
        });
        return new PlatformAiSettingsService(
            db,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            protection,
            openRouter,
            NullLogger<PlatformAiSettingsService>.Instance);
    }

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
    public async Task GetStudioAiModelRefAsync_WhenNoRow_ReturnsNull()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);

        var result = await service.GetStudioAiModelRefAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetStudioAiModelRefAsync_WhenConfigured_ReturnsNormalizedRef()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var row = PlatformAiSettings.CreateDefaults();
        row.SetStudioAiModel("qwen2.5:7b-instruct");
        db.PlatformAiSettings.Add(row);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.GetStudioAiModelRefAsync();

        Assert.Equal("qwen2.5:7b-instruct", result);
    }

    [Fact]
    public async Task SetStudioAiModelRefAsync_Clear_ReturnsNull()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);
        var actorId = Guid.NewGuid();

        await service.SetStudioAiModelRefAsync("ollama:qwen2.5:7b-instruct", actorId);
        var cleared = await service.SetStudioAiModelRefAsync("", actorId);
        var read = await service.GetStudioAiModelRefAsync();

        Assert.Null(cleared);
        Assert.Null(read);
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

    [Fact]
    public async Task SetOpenRouterConfigAsync_EncryptsAndMasksKey()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);
        var actorId = Guid.NewGuid();

        var (ok, error) = await service.SetOpenRouterConfigAsync(
            isEnabled: true,
            displayName: "OpenRouter Prod",
            baseUrl: "https://openrouter.ai/api/v1/",
            apiKey: "sk-or-v1-abcdefghij",
            actorId);

        Assert.True(ok);
        Assert.Null(error);

        var masked = await service.GetOpenRouterSettingsAsync();
        Assert.True(masked.IsEnabled);
        Assert.Equal("OpenRouter Prod", masked.DisplayName);
        Assert.Equal("https://openrouter.ai/api/v1", masked.BaseUrl);
        Assert.True(masked.IsApiKeyConfigured);
        Assert.Equal("ghij", masked.ApiKeyLast4);
        Assert.DoesNotContain("sk-or", masked.ApiKeyLast4 ?? "");

        var creds = await service.GetOpenRouterCredentialsAsync();
        Assert.True(creds.IsEnabled);
        Assert.Equal("sk-or-v1-abcdefghij", creds.ApiKey);
        Assert.Equal("https://openrouter.ai/api/v1", creds.BaseUrl);

        var stored = await db.PlatformAiSettings.AsNoTracking().SingleAsync();
        Assert.NotEqual("sk-or-v1-abcdefghij", stored.OpenRouterEncryptedApiKey);
    }

    [Fact]
    public async Task SetOpenRouterConfigAsync_EmptyApiKey_KeepsExistingSecret()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);
        var actorId = Guid.NewGuid();

        await service.SetOpenRouterConfigAsync(true, "OR", null, "sk-or-secret-key12", actorId);
        var before = await service.GetOpenRouterSettingsAsync();

        var (ok, error) = await service.SetOpenRouterConfigAsync(
            isEnabled: true,
            displayName: "OR Updated",
            baseUrl: null,
            apiKey: null,
            actorId);

        Assert.True(ok);
        Assert.Null(error);

        var after = await service.GetOpenRouterSettingsAsync();
        Assert.Equal("OR Updated", after.DisplayName);
        Assert.Equal(before.ApiKeyLast4, after.ApiKeyLast4);
        Assert.True(after.IsApiKeyConfigured);

        var creds = await service.GetOpenRouterCredentialsAsync();
        Assert.Equal("sk-or-secret-key12", creds.ApiKey);
    }

    [Fact]
    public async Task SetOpenRouterConfigAsync_EnableWithoutKey_Fails()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);

        var (ok, error) = await service.SetOpenRouterConfigAsync(
            isEnabled: true,
            displayName: "OR",
            baseUrl: null,
            apiKey: null,
            Guid.NewGuid());

        Assert.False(ok);
        Assert.Contains("clé API", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetCursorConfigAsync_EncryptsAndMasksKey()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);
        var actorId = Guid.NewGuid();

        var (ok, error) = await service.SetCursorConfigAsync(
            isEnabled: true,
            displayName: "Cursor Prod",
            apiKey: "cursor_abcdefghij",
            actorId);

        Assert.True(ok);
        Assert.Null(error);

        var masked = await service.GetCursorSettingsAsync();
        Assert.True(masked.IsEnabled);
        Assert.Equal("Cursor Prod", masked.DisplayName);
        Assert.True(masked.IsApiKeyConfigured);
        Assert.Equal("ghij", masked.ApiKeyLast4);
        Assert.DoesNotContain("cursor_", masked.ApiKeyLast4 ?? "");

        var creds = await service.GetCursorCredentialsAsync();
        Assert.True(creds.IsEnabled);
        Assert.Equal("cursor_abcdefghij", creds.ApiKey);

        var stored = await db.PlatformAiSettings.AsNoTracking().SingleAsync();
        Assert.NotEqual("cursor_abcdefghij", stored.CursorEncryptedApiKey);
    }

    [Fact]
    public async Task SetCursorConfigAsync_EmptyApiKey_KeepsExistingSecret()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);
        var actorId = Guid.NewGuid();

        await service.SetCursorConfigAsync(true, "Cursor", "cursor_secret-key12", actorId);
        var before = await service.GetCursorSettingsAsync();

        var (ok, error) = await service.SetCursorConfigAsync(
            isEnabled: true,
            displayName: "Cursor Updated",
            apiKey: null,
            actorId);

        Assert.True(ok);
        Assert.Null(error);

        var after = await service.GetCursorSettingsAsync();
        Assert.Equal("Cursor Updated", after.DisplayName);
        Assert.Equal(before.ApiKeyLast4, after.ApiKeyLast4);
        Assert.True(after.IsApiKeyConfigured);

        var creds = await service.GetCursorCredentialsAsync();
        Assert.Equal("cursor_secret-key12", creds.ApiKey);
    }

    [Fact]
    public async Task SetCursorConfigAsync_EnableWithoutKey_Fails()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new MasterDbContext(options);
        var service = CreateService(db);

        var (ok, error) = await service.SetCursorConfigAsync(
            isEnabled: true,
            displayName: "Cursor",
            apiKey: null,
            Guid.NewGuid());

        Assert.False(ok);
        Assert.Contains("clé API", error, StringComparison.OrdinalIgnoreCase);
    }
}
