using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class TenantAiProviderRepository : ITenantAiProviderRepository
{
    private const string ProviderKeyOpenRouter = "openrouter";
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly IDataProtector _protector;

    public TenantAiProviderRepository(
        ITenantDbContextFactory contextFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _contextFactory = contextFactory;
        _protector = dataProtectionProvider.CreateProtector("TenantAiProviderSecrets");
    }

    public async Task<TenantAiProvider?> GetByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.TenantAiProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProviderKey == providerKey, cancellationToken);
    }

    public async Task UpsertOpenRouterAsync(
        string? displayName,
        string? baseUrl,
        string? plainApiKey,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var existing = await context.TenantAiProviders
            .FirstOrDefaultAsync(p => p.ProviderKey == ProviderKeyOpenRouter, cancellationToken);

        var encrypted = existing?.EncryptedApiKey ?? _protector.Protect(string.Empty);
        if (!string.IsNullOrWhiteSpace(plainApiKey))
            encrypted = _protector.Protect(plainApiKey.Trim());

        if (existing is null)
        {
            context.TenantAiProviders.Add(TenantAiProvider.CreateOpenRouterDefault(
                encrypted,
                isEnabled,
                displayName,
                baseUrl));
        }
        else
        {
            existing.Update(
                string.IsNullOrWhiteSpace(displayName) ? existing.DisplayName : displayName.Trim(),
                baseUrl,
                encrypted,
                isEnabled);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetDecryptedApiKeyForOpenRouterAsync(CancellationToken cancellationToken = default)
    {
        var row = await GetByProviderKeyAsync(ProviderKeyOpenRouter, cancellationToken);
        if (row is null || !row.IsEnabled || string.IsNullOrEmpty(row.EncryptedApiKey))
            return null;
        try
        {
            var key = _protector.Unprotect(row.EncryptedApiKey);
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Decrypts API key for outbound HTTP calls.</summary>
    public static string? TryDecryptApiKey(IDataProtector protector, string encryptedBlob)
    {
        if (string.IsNullOrEmpty(encryptedBlob))
            return null;
        try
        {
            return protector.Unprotect(encryptedBlob);
        }
        catch
        {
            return null;
        }
    }
}
