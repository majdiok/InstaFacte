using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Channels.Bridge;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>État de liaison WhatsApp de l'utilisateur courant (page Paramètres → WhatsApp).</summary>
public sealed record ChannelStatusDto(
    bool Enabled,
    bool WhatsAppEnabled,
    bool Linked,
    string? LinkedNumberMasked,
    DateTime? VerifiedAtUtc);

/// <summary>Code de liaison fraîchement émis (affiché une seule fois, jamais persisté en clair).</summary>
public sealed record ChannelLinkCodeDto(string Code, DateTime ExpiresAtUtc, int TtlMinutes);

/// <summary>État du pont WhatsApp (section administration). <c>State</c> = nom de <c>BridgeState</c>.</summary>
public sealed record ChannelBridgeStatusDto(
    bool Enabled,
    bool WhatsAppEnabled,
    string State,
    string? LastError,
    DateTime SinceUtc);

/// <summary>QR de connexion en PNG base64 (data URI), ou null hors état « en attente de scan ».</summary>
public sealed record ChannelBridgeQrDto(string? QrPng);

/// <summary>
/// Liaison des canaux externes (WhatsApp) pour l'utilisateur courant : page personnelle — tout
/// utilisateur authentifié gère SA propre liaison (pas de policy admin). Les endpoints sont
/// inertes quand la fonctionnalité est désactivée (<c>Channels.Enabled</c>/<c>WhatsAppEnabled</c>).
/// </summary>
[ApiController]
[Route("api/channels")]
[Authorize]
public sealed class ChannelsController : ControllerBase
{
    private readonly IChannelLinkService _linkService;
    private readonly ICurrentUser _currentUser;
    private readonly IWhatsAppBridge _bridge;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly IOptions<ChannelsSettings> _settings;
    private readonly ILogger<ChannelsController> _logger;

    public ChannelsController(
        IChannelLinkService linkService,
        ICurrentUser currentUser,
        IWhatsAppBridge bridge,
        IQrCodeGenerator qrCodeGenerator,
        IOptions<ChannelsSettings> settings,
        ILogger<ChannelsController> logger)
    {
        _linkService = linkService;
        _currentUser = currentUser;
        _bridge = bridge;
        _qrCodeGenerator = qrCodeGenerator;
        _settings = settings;
        _logger = logger;
    }

    private ChannelsSettings Settings => _settings.Value;

    private bool FeatureEnabled => Settings.Enabled && Settings.WhatsAppEnabled;

    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChannelStatusDto>>> GetStatus(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.TenantId is not { } tenantId)
            return Unauthorized();

        if (!FeatureEnabled)
        {
            return Ok(ApiResponse<ChannelStatusDto>.Ok(
                new ChannelStatusDto(Settings.Enabled, Settings.WhatsAppEnabled, false, null, null)));
        }

        var status = await _linkService.GetLinkStatusAsync(tenantId, userId, ChannelType.WhatsApp, cancellationToken);
        return Ok(ApiResponse<ChannelStatusDto>.Ok(new ChannelStatusDto(
            Settings.Enabled,
            Settings.WhatsAppEnabled,
            status.Linked,
            status.ExternalUserIdMasked,
            status.VerifiedAtUtc)));
    }

    [HttpPost("link-code")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ChannelLinkCodeDto>>> GenerateLinkCode(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.TenantId is not { } tenantId)
            return Unauthorized();

        if (!FeatureEnabled)
            return NotFound(ApiResponse<ChannelLinkCodeDto>.Fail("Le canal WhatsApp n'est pas activé.", "CHANNELS_DISABLED"));

        var issue = await _linkService.GenerateLinkCodeAsync(tenantId, userId, ChannelType.WhatsApp, cancellationToken);
        _logger.LogInformation("Code de liaison WhatsApp émis pour l'utilisateur {UserId}.", userId);
        return Ok(ApiResponse<ChannelLinkCodeDto>.Ok(
            new ChannelLinkCodeDto(issue.Code, issue.ExpiresAtUtc, Settings.LinkCodeTtlMinutes)));
    }

    [HttpDelete("links/whatsapp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> Unlink(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.TenantId is not { } tenantId)
            return Unauthorized();

        if (!FeatureEnabled)
            return NotFound(ApiResponse<bool>.Fail("Le canal WhatsApp n'est pas activé.", "CHANNELS_DISABLED"));

        var unlinked = await _linkService.UnlinkByUserAsync(tenantId, userId, ChannelType.WhatsApp, cancellationToken);
        _logger.LogInformation("Déliaison WhatsApp demandée par l'utilisateur {UserId} : {Result}.", userId, unlinked);
        return Ok(ApiResponse<bool>.Ok(unlinked));
    }

    // ── Administration du pont WhatsApp (session partagée de l'installation) ──
    // Réservé aux Administrateurs. Limitation v1 documentée : le pont est unique pour l'installation ;
    // en multi-tenant, tout Administrateur pilote la même session WhatsApp.

    [HttpGet("bridge/status")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<ChannelBridgeStatusDto>> GetBridgeStatus()
    {
        var status = _bridge.Status;
        return Ok(ApiResponse<ChannelBridgeStatusDto>.Ok(new ChannelBridgeStatusDto(
            Settings.Enabled,
            Settings.WhatsAppEnabled,
            status.State.ToString(),
            status.LastError,
            status.SinceUtc)));
    }

    [HttpGet("bridge/qr")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<ChannelBridgeQrDto>> GetBridgeQr()
    {
        var status = _bridge.Status;
        if (status.State != BridgeState.WaitingQr || string.IsNullOrEmpty(status.Qr))
            return Ok(ApiResponse<ChannelBridgeQrDto>.Ok(new ChannelBridgeQrDto(null)));

        var png = _qrCodeGenerator.GeneratePng(status.Qr);
        var dataUri = png is null ? null : "data:image/png;base64," + Convert.ToBase64String(png);
        return Ok(ApiResponse<ChannelBridgeQrDto>.Ok(new ChannelBridgeQrDto(dataUri)));
    }

    [HttpPost("bridge/restart")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> RestartBridge()
    {
        if (!FeatureEnabled)
            return NotFound(ApiResponse<bool>.Fail("Le canal WhatsApp n'est pas activé.", "CHANNELS_DISABLED"));

        await _bridge.RestartAsync();
        _logger.LogInformation("Pont WhatsApp : redémarrage demandé par un administrateur.");
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("bridge/logout")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> LogoutBridge()
    {
        if (!FeatureEnabled)
            return NotFound(ApiResponse<bool>.Fail("Le canal WhatsApp n'est pas activé.", "CHANNELS_DISABLED"));

        await _bridge.LogoutAsync();
        _logger.LogInformation("Pont WhatsApp : déconnexion de session demandée par un administrateur.");
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
