using System.Security.Cryptography;
using System.Text;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities.Channels;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Channels;

/// <summary>
/// Implémentation de <see cref="IChannelLinkService"/>. Les tables tenant (source de vérité) sont
/// ouvertes directement depuis la chaîne de connexion (patron <c>FiscalReminderJob</c> — le flux
/// arrive par webhook anonyme ou job Hangfire, sans contexte tenant ambiant) ; l'index de routage
/// vit dans la base master. Écritures ordonnées tenant → master, compensation best-effort.
/// </summary>
public sealed class ChannelLinkService : IChannelLinkService
{
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    private readonly MasterDbContext _master;
    private readonly ITenantService _tenantService;
    private readonly TimeProvider _timeProvider;
    private readonly ChannelsSettings _settings;
    private readonly ILogger<ChannelLinkService> _logger;

    public ChannelLinkService(
        MasterDbContext master,
        ITenantService tenantService,
        TimeProvider timeProvider,
        IOptions<ChannelsSettings> settings,
        ILogger<ChannelLinkService> logger)
    {
        _master = master;
        _tenantService = tenantService;
        _timeProvider = timeProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ChannelLinkCodeIssue> GenerateLinkCodeAsync(
        Guid tenantId, Guid userId, ChannelType channelType, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var code = GenerateCode();
        var codeHash = HashCode(code);
        var expiresAt = now.AddMinutes(Math.Max(1, _settings.LinkCodeTtlMinutes));

        await using var tenantDb = await OpenTenantDbAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Chaîne de connexion introuvable pour le tenant {tenantId}.");

        // Invalide les codes en attente du couple (utilisateur, canal) — un seul code actif à la fois.
        var pendingCodes = await tenantDb.ChannelLinkCodes
            .Where(c => c.UserId == userId && c.ChannelType == channelType && c.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var pending in pendingCodes)
            pending.MarkConsumed(now);

        var linkCode = ChannelLinkCode.Create(userId, channelType, codeHash, expiresAt);
        tenantDb.ChannelLinkCodes.Add(linkCode);
        await tenantDb.SaveChangesAsync(cancellationToken);

        try
        {
            var pendingPointers = await _master.ChannelLinkCodePointers
                .Where(p => p.UserId == userId && p.ChannelType == channelType && p.ConsumedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var pending in pendingPointers)
                pending.MarkConsumed(now);

            _master.ChannelLinkCodePointers.Add(
                ChannelLinkCodePointer.Create(channelType, codeHash, tenantId, userId, expiresAt));
            await _master.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Compensation best-effort : sans pointeur master le code est inutilisable — on retire
            // la ligne tenant (un orphelin resterait de toute façon inerte : il expire).
            try
            {
                tenantDb.ChannelLinkCodes.Remove(linkCode);
                await tenantDb.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx,
                    "Compensation du code de liaison échouée pour le tenant {TenantId} (orphelin inerte).", tenantId);
            }

            throw;
        }

        return new ChannelLinkCodeIssue(code, expiresAt);
    }

    public async Task<ChannelLinkResult> TryConsumeLinkCodeAsync(
        ChannelType channelType, string rawCode, string externalUserId, string externalChatId,
        CancellationToken cancellationToken = default)
    {
        var normalized = Application.Features.Channels.ChannelCommandParser.NormalizeLinkCode(rawCode);
        if (normalized.Length == 0)
            return new ChannelLinkResult(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var codeHash = HashCode(normalized);
        var maxAttempts = Math.Max(1, _settings.LinkCodeMaxAttempts);

        var pointer = await _master.ChannelLinkCodePointers
            .FirstOrDefaultAsync(p => p.ChannelType == channelType && p.CodeHash == codeHash, cancellationToken);
        if (pointer is null || pointer.ConsumedAt is not null || pointer.ExpiresAt <= now || pointer.AttemptCount >= maxAttempts)
            return new ChannelLinkResult(false);

        await using var tenantDb = await OpenTenantDbAsync(pointer.TenantId, cancellationToken);
        if (tenantDb is null)
            return new ChannelLinkResult(false);

        // Re-validation côté tenant (source de vérité).
        var tenantCode = await tenantDb.ChannelLinkCodes
            .FirstOrDefaultAsync(c => c.ChannelType == channelType && c.CodeHash == codeHash, cancellationToken);
        if (tenantCode is null || tenantCode.ConsumedAt is not null || tenantCode.ExpiresAt <= now ||
            tenantCode.AttemptCount >= maxAttempts || tenantCode.UserId != pointer.UserId)
        {
            pointer.RegisterAttempt();
            tenantCode?.RegisterAttempt();
            await tenantDb.SaveChangesAsync(cancellationToken);
            await _master.SaveChangesAsync(cancellationToken);
            return new ChannelLinkResult(false);
        }

        var trimmedExternalUserId = externalUserId.Trim();

        // Ce numéro était-il déjà lié à un AUTRE utilisateur du même tenant ? L'index unique
        // (ChannelType, ExternalUserId) interdit deux lignes — on retire l'ancienne liaison.
        var conflicting = await tenantDb.ChannelIdentityLinks
            .FirstOrDefaultAsync(l => l.ChannelType == channelType && l.ExternalUserId == trimmedExternalUserId
                && l.UserId != pointer.UserId, cancellationToken);
        if (conflicting is not null)
            tenantDb.ChannelIdentityLinks.Remove(conflicting);

        // Re-lien du même utilisateur : l'index unique (UserId, ChannelType) impose Rebind (jamais
        // d'insertion d'une seconde ligne, même désactivée).
        var existing = await tenantDb.ChannelIdentityLinks
            .FirstOrDefaultAsync(l => l.UserId == pointer.UserId && l.ChannelType == channelType, cancellationToken);
        if (existing is not null)
            existing.Rebind(trimmedExternalUserId, externalChatId, now);
        else
            tenantDb.ChannelIdentityLinks.Add(
                ChannelIdentityLink.Create(pointer.UserId, channelType, trimmedExternalUserId, externalChatId, now));

        tenantCode.MarkConsumed(now);
        await tenantDb.SaveChangesAsync(cancellationToken);

        // Index de routage master : une seule route par identité externe (Rebind si le numéro
        // change de compte), plus consommation du pointeur.
        var route = await _master.ChannelExternalRoutes
            .FirstOrDefaultAsync(r => r.ChannelType == channelType && r.ExternalUserId == trimmedExternalUserId, cancellationToken);
        if (route is not null)
            route.Rebind(pointer.TenantId, pointer.UserId, now);
        else
            _master.ChannelExternalRoutes.Add(
                ChannelExternalRoute.Create(channelType, trimmedExternalUserId, pointer.TenantId, pointer.UserId));

        // L'utilisateur peut avoir une ancienne route sous un autre numéro : désactivation.
        var staleRoutes = await _master.ChannelExternalRoutes
            .Where(r => r.ChannelType == channelType && r.UserId == pointer.UserId
                && r.ExternalUserId != trimmedExternalUserId && r.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var stale in staleRoutes)
            stale.Deactivate(now);

        pointer.MarkConsumed(now);
        await _master.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Liaison canal {ChannelType} établie pour l'utilisateur {UserId} (tenant {TenantId}).",
            channelType, pointer.UserId, pointer.TenantId);
        return new ChannelLinkResult(true);
    }

    public async Task<ChannelRouteInfo?> FindActiveRouteAsync(
        ChannelType channelType, string externalUserId, CancellationToken cancellationToken = default)
    {
        var trimmed = externalUserId.Trim();
        var route = await _master.ChannelExternalRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ChannelType == channelType && r.ExternalUserId == trimmed && r.IsActive,
                cancellationToken);
        return route is null ? null : new ChannelRouteInfo(route.TenantId, route.UserId);
    }

    public async Task<bool> UnlinkByExternalUserAsync(
        ChannelType channelType, string externalUserId, CancellationToken cancellationToken = default)
    {
        var trimmed = externalUserId.Trim();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var route = await _master.ChannelExternalRoutes
            .FirstOrDefaultAsync(r => r.ChannelType == channelType && r.ExternalUserId == trimmed && r.IsActive,
                cancellationToken);
        if (route is null)
            return false;

        await using var tenantDb = await OpenTenantDbAsync(route.TenantId, cancellationToken);
        if (tenantDb is not null)
        {
            var link = await tenantDb.ChannelIdentityLinks
                .FirstOrDefaultAsync(l => l.ChannelType == channelType && l.ExternalUserId == trimmed && l.IsActive,
                    cancellationToken);
            if (link is not null)
            {
                link.Deactivate();
                await tenantDb.SaveChangesAsync(cancellationToken);
            }
        }

        route.Deactivate(now);
        await _master.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnlinkByUserAsync(
        Guid tenantId, Guid userId, ChannelType channelType, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var unlinked = false;

        await using var tenantDb = await OpenTenantDbAsync(tenantId, cancellationToken);
        if (tenantDb is not null)
        {
            var link = await tenantDb.ChannelIdentityLinks
                .FirstOrDefaultAsync(l => l.UserId == userId && l.ChannelType == channelType && l.IsActive,
                    cancellationToken);
            if (link is not null)
            {
                link.Deactivate();
                await tenantDb.SaveChangesAsync(cancellationToken);
                unlinked = true;
            }
        }

        var routes = await _master.ChannelExternalRoutes
            .Where(r => r.UserId == userId && r.ChannelType == channelType && r.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var route in routes)
        {
            route.Deactivate(now);
            unlinked = true;
        }

        if (routes.Count > 0)
            await _master.SaveChangesAsync(cancellationToken);

        return unlinked;
    }

    public async Task<ChannelLinkStatus> GetLinkStatusAsync(
        Guid tenantId, Guid userId, ChannelType channelType, CancellationToken cancellationToken = default)
    {
        await using var tenantDb = await OpenTenantDbAsync(tenantId, cancellationToken);
        if (tenantDb is null)
            return new ChannelLinkStatus(false, null, null);

        var link = await tenantDb.ChannelIdentityLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.ChannelType == channelType && l.IsActive,
                cancellationToken);
        return link is null
            ? new ChannelLinkStatus(false, null, null)
            : new ChannelLinkStatus(true, MaskExternalUserId(link.ExternalUserId), link.VerifiedAt);
    }

    public async Task<ChannelInboundRegistration> TryRegisterInboundMessageAsync(
        Guid tenantId, ChannelType channelType, string externalMessageId, string externalUserId,
        Guid userId, string traceId, CancellationToken cancellationToken = default)
    {
        await using var tenantDb = await OpenTenantDbAsync(tenantId, cancellationToken);
        if (tenantDb is null)
            return ChannelInboundRegistration.LinkMissing;

        var trimmedExternal = externalUserId.Trim();

        // Re-validation systématique du lien tenant (la route master n'est qu'un index dénormalisé).
        var linkValid = await tenantDb.ChannelIdentityLinks
            .AsNoTracking()
            .AnyAsync(l => l.ChannelType == channelType && l.ExternalUserId == trimmedExternal
                && l.UserId == userId && l.IsActive, cancellationToken);
        if (!linkValid)
            return ChannelInboundRegistration.LinkMissing;

        var trimmedMessageId = externalMessageId.Trim();
        var alreadyProcessed = await tenantDb.ChannelInboundMessageLogs
            .AsNoTracking()
            .AnyAsync(m => m.ChannelType == channelType && m.ExternalMessageId == trimmedMessageId, cancellationToken);
        if (alreadyProcessed)
            return ChannelInboundRegistration.Duplicate;

        tenantDb.ChannelInboundMessageLogs.Add(
            ChannelInboundMessageLog.Create(channelType, trimmedMessageId, trimmedExternal, userId, traceId));
        try
        {
            await tenantDb.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Violation de l'index unique (ChannelType, ExternalMessageId) : doublon concurrent.
            return ChannelInboundRegistration.Duplicate;
        }

        return ChannelInboundRegistration.Registered;
    }

    public async Task TouchLastSeenAsync(
        Guid tenantId, ChannelType channelType, string externalUserId, CancellationToken cancellationToken = default)
    {
        await using var tenantDb = await OpenTenantDbAsync(tenantId, cancellationToken);
        if (tenantDb is null)
            return;

        var trimmed = externalUserId.Trim();
        var link = await tenantDb.ChannelIdentityLinks
            .FirstOrDefaultAsync(l => l.ChannelType == channelType && l.ExternalUserId == trimmed && l.IsActive,
                cancellationToken);
        if (link is null)
            return;

        link.TouchLastSeen(_timeProvider.GetUtcNow().UtcDateTime);
        await tenantDb.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Hash SHA-256 hexadécimal (majuscules) d'un code normalisé.</summary>
    public static string HashCode(string normalizedCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode)));

    /// <summary>Masque une identité externe : ne conserve que les 4 derniers chiffres du numéro.</summary>
    public static string MaskExternalUserId(string externalUserId)
    {
        var digits = new string(externalUserId.TakeWhile(c => c != '@').Where(char.IsDigit).ToArray());
        return digits.Length <= 4
            ? $"•••• {digits}"
            : $"•••• {digits[^4..]}";
    }

    private static string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }

    private async Task<TenantDbContext?> OpenTenantDbAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connectionString = await _tenantService.GetConnectionStringAsync(tenantId, cancellationToken);
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Chaîne de connexion introuvable pour le tenant {TenantId}.", tenantId);
            return null;
        }

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName))
            .Options;
        return new TenantDbContext(options);
    }
}
