using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FactuTrust.API.Http;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Logging;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly MasterDbContext _masterContext;
    private readonly ITenantService _tenantService;
    private readonly ITenantAuthTokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IEffectivePermissionService _effectivePermissionService;
    private readonly IAccountingFirmsFeature _accountingFirmsFeature;
    private readonly IAccountingFirmRegistrationService _accountingFirmRegistrationService;
    private readonly FirmGovernanceOptions _firmGovernanceOptions;
    private readonly AccountingFirmsOptions _accountingFirmsOptions;
    private readonly IEmailService _emailService;
    private readonly IClientPortalService _clientPortalService;
    private readonly IRegistrationSectorService _registrationSectorService;
    private readonly ISectorCatalogProvider _sectorCatalogProvider;
    private readonly RegistrationSectorOptions _registrationSectorOptions;
    private readonly EmailVerificationOptions _emailVerificationOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        MasterDbContext masterContext,
        ITenantService tenantService,
        ITenantAuthTokenService tokenService,
        IConfiguration configuration,
        IEffectivePermissionService effectivePermissionService,
        IAccountingFirmsFeature accountingFirmsFeature,
        IAccountingFirmRegistrationService accountingFirmRegistrationService,
        IOptions<FirmGovernanceOptions> firmGovernanceOptions,
        IOptions<AccountingFirmsOptions> accountingFirmsOptions,
        IEmailService emailService,
        IClientPortalService clientPortalService,
        IRegistrationSectorService registrationSectorService,
        ISectorCatalogProvider sectorCatalogProvider,
        IOptions<RegistrationSectorOptions> registrationSectorOptions,
        IOptions<EmailVerificationOptions> emailVerificationOptions,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _masterContext = masterContext;
        _tenantService = tenantService;
        _tokenService = tokenService;
        _configuration = configuration;
        _effectivePermissionService = effectivePermissionService;
        _accountingFirmsFeature = accountingFirmsFeature;
        _accountingFirmRegistrationService = accountingFirmRegistrationService;
        _firmGovernanceOptions = firmGovernanceOptions.Value;
        _accountingFirmsOptions = accountingFirmsOptions.Value;
        _emailService = emailService;
        _clientPortalService = clientPortalService;
        _registrationSectorService = registrationSectorService;
        _sectorCatalogProvider = sectorCatalogProvider;
        _registrationSectorOptions = registrationSectorOptions.Value;
        _emailVerificationOptions = emailVerificationOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user and create their company.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var totalSw = Stopwatch.StartNew();

        var validationSw = Stopwatch.StartNew();
        var nifResult = NIF.Create(dto.Nif);
        if (nifResult.IsFailure)
            return BadRequest(ApiResponse<AuthResponseDto>.Fail(nifResult.Error.Description));

        var addressResult = Address.Create(dto.Street, dto.City, dto.Governorate, dto.StreetLine2, dto.PostalCode);
        if (addressResult.IsFailure)
            return BadRequest(ApiResponse<AuthResponseDto>.Fail(addressResult.Error.Description));

        var emailResult = Email.Create(dto.CompanyEmail);
        if (emailResult.IsFailure)
            return BadRequest(ApiResponse<AuthResponseDto>.Fail(emailResult.Error.Description));

        var phoneResult = PhoneNumber.Create(dto.Phone);
        if (phoneResult.IsFailure)
            return BadRequest(ApiResponse<AuthResponseDto>.Fail(phoneResult.Error.Description));

        // Plan §1.2 (no silent rejections): reject unknown module ids up front with a clear 400
        // instead of letting SectorModuleSetCalculator drop them silently. Honoraires (firm-native,
        // never offered through the wizard) is a DEFINED enum value so it is NOT caught here — it is
        // still dropped downstream by design and surfaced as a non-blocking warning instead of a 400.
        if (dto.EnabledModules is { Count: > 0 })
        {
            var invalidModuleIds = dto.EnabledModules
                .Where(id => !Enum.IsDefined(typeof(AppModule), id))
                .Distinct()
                .ToList();
            if (invalidModuleIds.Count > 0)
            {
                return BadRequest(ApiResponse<AuthResponseDto>.Fail(
                    $"Modules invalides : {string.Join(", ", invalidModuleIds)}. Veuillez sélectionner des modules valides."));
            }
        }

        // Sector-aware registration wizard (plan §3 C1/C5, §6.1 B5) — optional fields; a legacy
        // payload (both codes absent) resolves to a null profile and behaves byte-identically to
        // today.
        var sectorProfileResult = _registrationSectorService.ResolveProfile(dto.CompanySegment, dto.BusinessDomain);
        if (sectorProfileResult.IsFailure)
            return BadRequest(ApiResponse<AuthResponseDto>.Fail(sectorProfileResult.Error.Description));
        var sectorProfile = sectorProfileResult.Value;

        // Plan §1.2: the sector kill-switch silently returns a null profile — surface it as a
        // non-blocking warning rather than pretending the client's segment/domain choice was applied.
        var warnings = new List<string>();
        if (!_registrationSectorOptions.Enabled
            && (!string.IsNullOrWhiteSpace(dto.CompanySegment) || !string.IsNullOrWhiteSpace(dto.BusinessDomain)))
        {
            warnings.Add("La sélection du type de société/domaine d'activité n'a pas été appliquée (fonctionnalité désactivée) ; la configuration par défaut est active.");
        }

        LogCompanyRegistrationStep("Validation", validationSw.ElapsedMilliseconds, null, correlationId);

        var emailCheckSw = Stopwatch.StartNew();
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        LogCompanyRegistrationStep("EmailPrecheck", emailCheckSw.ElapsedMilliseconds, null, correlationId);
        if (existingUser is not null)
            return BadRequest(ApiResponse<AuthResponseDto>.Fail("Un compte existe déjà avec cette adresse e-mail."));

        // Plan §1.4 (NIF uniqueness): pre-check before opening the transaction so the common case
        // returns a clean 409 without ever touching the database write path. Mirrors the
        // IsActive-scoped uniqueness FirmManagedClientService already enforces, and the filtered
        // unique index (MasterDbContext) that backstops the race between this check and the insert.
        const string duplicateNifMessage =
            "Une société avec ce matricule fiscal existe déjà. Contactez le support si vous pensez qu'il s'agit d'une erreur.";
        var nifCheckSw = Stopwatch.StartNew();
        var nifValue = nifResult.Value.Value;
        var nifExists = await _masterContext.Tenants.AsNoTracking()
            .AnyAsync(t => t.IsActive && t.NIF.Value == nifValue, cancellationToken);
        LogCompanyRegistrationStep("NifPrecheck", nifCheckSw.ElapsedMilliseconds, null, correlationId);
        if (nifExists)
            return Conflict(ApiResponse<AuthResponseDto>.Fail(duplicateNifMessage));

        string? provisionedDatabaseName = null;
        Guid? tenantId = null;

        await using var transaction = await _masterContext.Database.BeginTransactionAsync(cancellationToken);

        Tenant tenant;
        ApplicationUser user;

        try
        {
            var tenantResult = Tenant.Create(
                dto.CompanyName,
                nifResult.Value,
                addressResult.Value,
                emailResult.Value,
                phoneResult.Value,
                dto.TaxRegime,
                dto.Website);

            if (tenantResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(ApiResponse<AuthResponseDto>.Fail(tenantResult.Error.Description));
            }

            tenant = tenantResult.Value;
            if (tenant.Id == Guid.Empty)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(ApiResponse<AuthResponseDto>.Fail("Erreur interne : identifiant entreprise invalide."));
            }

            tenantId = tenant.Id;
            provisionedDatabaseName = tenant.DatabaseName;
            // Persist the RESOLVED profile's codes, never the raw dto values: when the
            // Features:RegistrationSector kill-switch is off (or the payload didn't resolve to a
            // known profile) sectorProfile is null and both columns stay NULL — this is the
            // kill-switch gate for storage, mirroring the gate already applied in ResolveProfile.
            tenant.SetSectorClassification(sectorProfile?.SegmentCode, sectorProfile?.DomainCode);

            // Plan §2.1 — records the catalog version tag active when the classification above was
            // resolved (null when sectorProfile itself is null, matching CompanySegment/BusinessDomain
            // staying null in that same case: kill-switch off, or an unresolved/legacy payload).
            if (sectorProfile is not null)
            {
                var catalogSnapshot = _sectorCatalogProvider.GetSnapshot();
                tenant.SetSectorCatalogVersion(catalogSnapshot.CatalogVersionTag);
            }

            var masterSw = Stopwatch.StartNew();
            _masterContext.Tenants.Add(tenant);

            var subscription = Subscription.CreateFree(tenant.Id);
            _masterContext.Subscriptions.Add(subscription);
            await _masterContext.SaveChangesAsync(cancellationToken);
            LogCompanyRegistrationStep("MasterEntities", masterSw.ElapsedMilliseconds, tenant.Id, correlationId);

            user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                TenantId = tenant.Id,
                // Plan §1.6 (D3 "mode doux") : Enabled=false (défaut) conserve le comportement
                // historique EmailConfirmed=true ; à true, la vérification devient effective et
                // l'email est envoyé après la fin de la mini-saga (voir plus bas), sans bloquer la
                // connexion tant que BlockLoginIfUnverified reste à false.
                EmailConfirmed = !_emailVerificationOptions.Enabled
            };
            user.ApplyNewInteractiveProductOnboarding();

            var identitySw = Stopwatch.StartNew();
            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                var errors = IdentityErrorTranslator.TranslateToFrench(createResult.Errors);
                return BadRequest(ApiResponse<AuthResponseDto>.Fail(errors));
            }

            await _userManager.AddToRoleAsync(user, UserRole.Administrator.ToString());
            LogCompanyRegistrationStep("IdentityCreate", identitySw.ElapsedMilliseconds, tenant.Id, correlationId);

            // Sector-aware registration wizard (plan §3 C4, §6.1 B5) — restriction-only; null/empty
            // dto.EnabledModules writes no grant rows (exact legacy "all modules enabled" behavior).
            // Rides this same transaction/SaveChanges — no separate commit. Plan §1.2: the outcome is
            // inspected below (after commit) to build non-blocking warnings for the response.
            var moduleOutcome = await _registrationSectorService.ApplyModuleSelectionAsync(
                user.Id,
                sectorProfile,
                dto.EnabledModules,
                SubscriptionPlan.Free,
                cancellationToken);

            await _masterContext.SaveChangesAsync(cancellationToken);

            // Plan §1.5 (provisioning mini-saga): commit the master rows (tenant starts
            // ProvisioningStatus=Pending) BEFORE the tenant database is provisioned. If provisioning
            // fails afterwards, these master rows stay committed as a Failed tenant (cleaned up by
            // OrphanTenantDatabaseCleanupJob) instead of being silently rolled back — so a failed
            // provisioning attempt is always observable, and the physical tenant database it may have
            // partially created is never left with zero trace in the master DB.
            await transaction.CommitAsync(cancellationToken);

            AppendModuleSelectionWarnings(warnings, moduleOutcome, tenant.Id, correlationId);
        }
        catch (DbUpdateException ex) when (IsNifUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(
                "Registration rejected: concurrent duplicate NIF for {Email}. CorrelationId={CorrelationId}",
                LogSanitizer.MaskEmail(dto.Email),
                correlationId);
            return Conflict(ApiResponse<AuthResponseDto>.Fail(duplicateNifMessage));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            if (!string.IsNullOrEmpty(provisionedDatabaseName))
                await _tenantService.TryDropDatabaseAsync(provisionedDatabaseName, cancellationToken);

            _logger.LogError(
                ex,
                "Registration failed for {Email}. CorrelationId: {CorrelationId}. TenantId: {TenantId}. Detail: {Detail}",
                LogSanitizer.MaskEmail(dto.Email),
                correlationId,
                tenantId,
                ex.InnerException?.Message ?? ex.Message);

            const string userMessage =
                "L'inscription n'a pas pu être finalisée. Réessayez plus tard ou contactez le support.";
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<AuthResponseDto>.Fail(userMessage, $"REG-{correlationId}"));
        }

        // Plan §1.5 — phase 2 of the mini-saga: the master transaction above already committed with
        // the tenant Pending, so a failure from here on never rolls back or hides the Failed tenant —
        // it is left for the orphan cleanup job, and the client is asked to retry (not silently given
        // a broken/half-provisioned account).
        try
        {
            var provisionSw = Stopwatch.StartNew();
            var effectiveWarehouseName = !string.IsNullOrWhiteSpace(dto.WarehouseName)
                ? dto.WarehouseName
                : sectorProfile?.DefaultWarehouseName;
            await _tenantService.CreateTenantDatabaseAsync(tenant.Id, tenant.DatabaseName, effectiveWarehouseName, cancellationToken);
            LogCompanyRegistrationStep("DatabaseProvision", provisionSw.ElapsedMilliseconds, tenant.Id, correlationId);

            tenant.MarkProvisioningReady();
            await _masterContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tenant database provisioning failed after master commit for {Email}. CorrelationId: {CorrelationId}. TenantId: {TenantId}. Detail: {Detail}",
                LogSanitizer.MaskEmail(dto.Email),
                correlationId,
                tenant.Id,
                ex.InnerException?.Message ?? ex.Message);

            try
            {
                tenant.MarkProvisioningFailed();
                await _masterContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception markFailedEx)
            {
                _logger.LogError(
                    markFailedEx,
                    "Failed to mark tenant {TenantId} as provisioning-failed. CorrelationId: {CorrelationId}",
                    tenant.Id,
                    correlationId);
            }

            await _tenantService.TryDropDatabaseAsync(tenant.DatabaseName, cancellationToken);

            const string userMessage =
                "L'inscription n'a pas pu être finalisée. Réessayez plus tard ou contactez le support.";
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<AuthResponseDto>.Fail(userMessage, $"REG-{correlationId}"));
        }

        if (_emailVerificationOptions.Enabled)
            await SendVerificationEmailAsync(user, cancellationToken);

        var tokenSw = Stopwatch.StartNew();
        var tokens = await _tokenService.GenerateTokensAsync(user.Id, tenant.Id, cancellationToken: cancellationToken);
        LogCompanyRegistrationStep("TokenGeneration", tokenSw.ElapsedMilliseconds, tenant.Id, correlationId);

        if (warnings.Count > 0)
            tokens = tokens with { Warnings = warnings };

        totalSw.Stop();
        LogCompanyRegistrationStep("Total", totalSw.ElapsedMilliseconds, tenant.Id, correlationId);

        _logger.LogInformation(
            "User {Email} registered with tenant {TenantId}. CorrelationId={CorrelationId} DurationMs={DurationMs}",
            LogSanitizer.MaskEmail(dto.Email),
            tenant.Id,
            correlationId,
            totalSw.ElapsedMilliseconds);

        return CreatedAtAction(nameof(Login), ApiResponse<AuthResponseDto>.Ok(tokens, "Inscription réussie"));
    }

    /// <summary>Plan §1.2 — turns a <see cref="ModuleSelectionOutcome"/> into French, non-blocking warnings (also logged).</summary>
    private void AppendModuleSelectionWarnings(List<string> warnings, ModuleSelectionOutcome outcome, Guid tenantId, string correlationId)
    {
        if (outcome.Ignored)
        {
            warnings.Add("La sélection de modules n'a pas été appliquée (fonctionnalité désactivée) ; les modules par défaut sont actifs.");
            _logger.LogWarning(
                "Register: module selection ignored (kill-switch off) for tenant {TenantId}. CorrelationId={CorrelationId}",
                tenantId,
                correlationId);
        }

        if (outcome.DeniedByPlan.Count > 0)
        {
            var deniedNames = string.Join(", ", outcome.DeniedByPlan.Select(m => m.ToDisplayString()));
            warnings.Add($"Certains modules choisis ne sont pas inclus dans votre offre actuelle et n'ont pas été activés : {deniedNames}.");
            _logger.LogWarning(
                "Register: modules denied by plan for tenant {TenantId}: {Modules}. CorrelationId={CorrelationId}",
                tenantId,
                deniedNames,
                correlationId);
        }

        if (outcome.DroppedInvalidIds.Count > 0)
        {
            warnings.Add("Certains modules sélectionnés ne sont pas disponibles à l'inscription et ont été ignorés.");
            _logger.LogWarning(
                "Register: dropped invalid/disallowed module id(s) for tenant {TenantId}: {Ids}. CorrelationId={CorrelationId}",
                tenantId,
                string.Join(",", outcome.DroppedInvalidIds),
                correlationId);
        }
    }

    /// <summary>Plan §1.4 — detects the filtered unique index violation on Tenants.NIF (race between the pre-check and the insert).</summary>
    private static bool IsNifUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not Microsoft.Data.SqlClient.SqlException sqlEx)
            return false;

        // 2601 = "Cannot insert duplicate key row" (unique index); 2627 = "Violation of UNIQUE KEY constraint".
        return (sqlEx.Number is 2601 or 2627) && sqlEx.Message.Contains("IX_Tenants_NIF", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Plan §1.6 — generates an ASP.NET Core Identity email-confirmation token (default 24h
    /// lifespan, no custom <c>TokenLifespan</c> configured) and emails the verification link.
    /// Never throws: a delivery failure must not fail registration or the resend endpoint's
    /// caller — it is logged and the caller still gets its generic success response, exactly like
    /// <see cref="ForgotPassword"/> already does for password reset emails.
    /// </summary>
    private async Task SendVerificationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var frontendBase = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
            var verifyUrl =
                $"{frontendBase}/auth/verify-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

            var recipientName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(recipientName))
                recipientName = user.Email!;

            var htmlBody = $"""
                <p>Bonjour <strong>{System.Net.WebUtility.HtmlEncode(recipientName)}</strong>,</p>
                <p>Merci de vous être inscrit sur InstaFact. Confirmez votre adresse email pour finaliser votre inscription.</p>
                <p><a href="{verifyUrl}" style="display:inline-block;padding:10px 20px;background:#2563eb;color:white;text-decoration:none;border-radius:6px;">Vérifier mon adresse email</a></p>
                <p style="color:#666;font-size:12px;">Si vous n'êtes pas à l'origine de cette inscription, ignorez cet email. Ce lien expire après 24 heures.</p>
                """;

            await _emailService.SendEmailAsync(
                user.Email!,
                "Vérifiez votre adresse email InstaFact",
                htmlBody,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Verification email queued for {Email}", LogSanitizer.MaskEmail(user.Email!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email for {Email}", LogSanitizer.MaskEmail(user.Email ?? "<null>"));
        }
    }

    /// <summary>
    /// Confirms a user's email using the token sent by <see cref="SendVerificationEmailAsync"/>
    /// (plan §1.6). No-ops (still returns success) if the account is already confirmed, so
    /// double-clicking the email link or replaying an old link on an already-verified account is
    /// harmless.
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("email-verification")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto, CancellationToken cancellationToken)
    {
        if (!_emailVerificationOptions.Enabled)
            return NotFound(ApiResponse<object>.Fail("La vérification d'email n'est pas activée."));

        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Token))
            return BadRequest(ApiResponse<object>.Fail("Email et jeton de vérification requis."));

        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (user is null)
            return BadRequest(ApiResponse<object>.Fail("Jeton de vérification invalide ou expiré."));

        if (user.EmailConfirmed)
            return Ok(ApiResponse<object>.Ok(null!, "Votre adresse email est déjà vérifiée."));

        var result = await _userManager.ConfirmEmailAsync(user, dto.Token);
        if (!result.Succeeded)
        {
            var errors = IdentityErrorTranslator.TranslateToFrench(result.Errors);
            return BadRequest(ApiResponse<object>.Fail(errors));
        }

        _logger.LogInformation("Email verified for {Email}", LogSanitizer.MaskEmail(dto.Email));

        return Ok(ApiResponse<object>.Ok(null!, "Votre adresse email a été vérifiée avec succès."));
    }

    /// <summary>
    /// Re-sends the verification email (plan §1.6). Always returns the same generic success
    /// message regardless of whether the account exists, to avoid email enumeration — mirrors
    /// <see cref="ForgotPassword"/>.
    /// </summary>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("email-verification")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto, CancellationToken cancellationToken)
    {
        if (!_emailVerificationOptions.Enabled)
            return NotFound(ApiResponse<object>.Fail("La vérification d'email n'est pas activée."));

        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(ApiResponse<object>.Fail("L'adresse email est requise."));

        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (user is not null && user.IsActive && !user.EmailConfirmed)
            await SendVerificationEmailAsync(user, cancellationToken);

        return Ok(ApiResponse<object>.Ok(
            null!,
            "Si un compte non vérifié existe avec cette adresse email, un nouvel email de vérification a été envoyé."));
    }

    private void LogCompanyRegistrationStep(string step, long durationMs, Guid? tenantId, string correlationId)
    {
        _logger.LogInformation(
            "CompanyRegistration.Step={Step} DurationMs={DurationMs} TenantId={TenantId} CorrelationId={CorrelationId}",
            step,
            durationMs,
            tenantId,
            correlationId);
    }

    /// <summary>
    /// Authenticate user and return JWT tokens.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Login failed for {Email}: user not found or inactive", LogSanitizer.MaskEmail(dto.Email));
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Email ou mot de passe incorrect"));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Email} is locked out", LogSanitizer.MaskEmail(dto.Email));
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Compte verrouillé. Réessayez dans 15 minutes."));
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed for {Email}: invalid password", LogSanitizer.MaskEmail(dto.Email));
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Email ou mot de passe incorrect"));
        }

        var portalGate = await _clientPortalService.EnsurePortalLoginAllowedAsync(user.Id, cancellationToken);
        if (portalGate.IsFailure)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AuthResponseDto>.Fail(portalGate.Error.Description));

        // Check 2FA
        if (result.RequiresTwoFactor)
        {
            return Ok(ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Requires2Fa = true,
                User = await BuildUserProfileDtoAsync(user, null, cancellationToken)
            }));
        }

        if (user.TenantId == Guid.Empty)
        {
            _logger.LogWarning("User {Email} has no company (TenantId empty)", LogSanitizer.MaskEmail(user.Email));
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Aucune entreprise associée à ce compte. Veuillez contacter l'administrateur."));
        }

        var tenant = await _masterContext.Tenants.FindAsync(new object[] { user.TenantId }, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Entreprise inactive ou introuvable"));
        }

        // Plan §1.5 (provisioning mini-saga): a Pending/Failed tenant has no working database yet
        // (provisioning still running, or it failed and is queued for orphan cleanup) — refuse login
        // with a clear message instead of letting the request through to a broken tenant context.
        if (tenant.ProvisioningStatus != TenantProvisioningStatus.Ready)
        {
            _logger.LogWarning(
                "Login refused for {Email}: tenant {TenantId} provisioning status is {Status}",
                LogSanitizer.MaskEmail(dto.Email),
                tenant.Id,
                tenant.ProvisioningStatus);
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail(
                "La configuration de votre entreprise est en cours de finalisation. Réessayez dans quelques instants ou contactez le support si le problème persiste."));
        }

        // Plan §1.6 (D3 "mode doux") : ne bloque la connexion que si les deux flags sont actifs.
        // Enabled=false (défaut) ou BlockLoginIfUnverified=false laisse passer, quel que soit
        // EmailConfirmed — ce qui couvre aussi tous les comptes créés avant l'activation du flag.
        if (_emailVerificationOptions.Enabled && _emailVerificationOptions.BlockLoginIfUnverified && !user.EmailConfirmed)
        {
            _logger.LogWarning("Login refused for {Email}: email not verified.", LogSanitizer.MaskEmail(dto.Email));
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail(
                "Veuillez vérifier votre adresse email avant de vous connecter. Consultez votre boîte de réception ou demandez un nouvel email de vérification."));
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var tokens = await _tokenService.GenerateTokensAsync(user.Id, tenant.Id, cancellationToken: cancellationToken);

        _logger.LogInformation("User {Email} logged in successfully", LogSanitizer.MaskEmail(dto.Email));

        return Ok(ApiResponse<AuthResponseDto>.Ok(tokens, "Connexion réussie"));
    }

    /// <summary>
    /// Request a password reset link by email (always returns success to prevent email enumeration).
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(ApiResponse<object>.Fail("L'adresse email est requise."));

        var email = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is not null && user.IsActive)
        {
            var tenantActive = user.TenantId != Guid.Empty;
            if (tenantActive)
            {
                var tenant = await _masterContext.Tenants.FindAsync(new object[] { user.TenantId }, cancellationToken);
                tenantActive = tenant is not null && tenant.IsActive;
            }

            if (tenantActive)
            {
                try
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var frontendBase = _configuration["App:FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
                    var resetUrl =
                        $"{frontendBase}/auth/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

                    var recipientName = $"{user.FirstName} {user.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(recipientName))
                        recipientName = email;

                    var htmlBody = $"""
                        <p>Bonjour <strong>{System.Net.WebUtility.HtmlEncode(recipientName)}</strong>,</p>
                        <p>Vous avez demandé la réinitialisation de votre mot de passe InstaFact.</p>
                        <p><a href="{resetUrl}" style="display:inline-block;padding:10px 20px;background:#2563eb;color:white;text-decoration:none;border-radius:6px;">Réinitialiser mon mot de passe</a></p>
                        <p style="color:#666;font-size:12px;">Si vous n'êtes pas à l'origine de cette demande, ignorez cet email. Ce lien expire après un court délai.</p>
                        """;

                    await _emailService.SendEmailAsync(
                        email,
                        "Réinitialisation de votre mot de passe InstaFact",
                        htmlBody,
                        cancellationToken: cancellationToken);

                    _logger.LogInformation("Password reset email queued for {Email}", LogSanitizer.MaskEmail(email));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset email for {Email}", LogSanitizer.MaskEmail(email));
                }
            }
        }

        return Ok(ApiResponse<object>.Ok(
            null!,
            "Si un compte existe avec cette adresse email, un lien de réinitialisation a été envoyé."));
    }

    /// <summary>
    /// Reset password using token from email link.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Token))
            return BadRequest(ApiResponse<object>.Fail("Email et jeton de réinitialisation requis."));

        if (string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(ApiResponse<object>.Fail("Le nouveau mot de passe est requis."));

        if (!string.Equals(dto.NewPassword, dto.ConfirmNewPassword, StringComparison.Ordinal))
            return BadRequest(ApiResponse<object>.Fail("Les mots de passe ne correspondent pas."));

        var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (user is null)
            return BadRequest(ApiResponse<object>.Fail("Jeton de réinitialisation invalide ou expiré."));

        var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
        {
            var errors = IdentityErrorTranslator.TranslateToFrench(result.Errors);
            return BadRequest(ApiResponse<object>.Fail(errors));
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password reset successful for {Email}", LogSanitizer.MaskEmail(dto.Email));

        return Ok(ApiResponse<object>.Ok(null!, "Votre mot de passe a été réinitialisé avec succès."));
    }

    /// <summary>
    /// Refresh access token using refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto, CancellationToken cancellationToken)
    {
        var refreshTokenHash = RefreshTokenHasher.Hash(dto.RefreshToken);
        var user = await _masterContext.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshTokenHash, cancellationToken);

        // Compatibilité : tokens émis avant le passage au stockage haché (rotation ré-écrit en hash).
        // À retirer après le 2026-07-31 (durée de vie max des refresh tokens : 7 jours).
        user ??= await _masterContext.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken, cancellationToken);

        if (user is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Token de rafraîchissement invalide ou expiré"));
        }

        if (user.TenantId == Guid.Empty)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Aucune entreprise associée à ce compte. Veuillez contacter l'administrateur."));
        }

        var tenant = await _masterContext.Tenants.FindAsync(new object[] { user.TenantId }, cancellationToken);
        if (tenant is null)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Entreprise introuvable"));
        }

        var portalGate = await _clientPortalService.EnsurePortalLoginAllowedAsync(user.Id, cancellationToken);
        if (portalGate.IsFailure)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AuthResponseDto>.Fail(portalGate.Error.Description));

        var tokens = await _tokenService.GenerateTokensAsync(user.Id, tenant.Id, cancellationToken: cancellationToken);

        return Ok(ApiResponse<AuthResponseDto>.Ok(tokens));
    }

    /// <summary>
    /// Logout and invalidate refresh token.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null && Guid.TryParse(userId, out var id))
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _userManager.UpdateAsync(user);
            }
        }

        return Ok(ApiResponse<object>.Ok(null!, "Déconnexion réussie"));
    }

    /// <summary>
    /// Returns the current authenticated user with fresh permissions and modules
    /// computed from the database, without rotating the JWT or refresh token.
    /// Used by the front-end at app boot to re-sync the locally stored user with
    /// backend permission/module changes (role updates, grant changes) without
    /// forcing a logout.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("Identité utilisateur invalide"));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("Utilisateur introuvable"));
        }

        Tenant? tenant = null;
        if (user.TenantId != Guid.Empty)
        {
            tenant = await _masterContext.Tenants.FindAsync(new object[] { user.TenantId }, cancellationToken);
        }

        var dto = await BuildUserProfileDtoAsync(user, tenant, cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(dto));
    }

    /// <summary>
    /// Register a new accounting firm and its manager user.
    /// </summary>
    [HttpPost("register-firm")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterFirm([FromBody] RegisterAccountingFirmDto dto, CancellationToken cancellationToken)
    {
        if (!_accountingFirmsFeature.IsEnabled)
            return NotFound();

        var result = await _accountingFirmRegistrationService.RegisterAsync(dto, cancellationToken);

        if (result.Success && result.Tokens is not null)
        {
            return CreatedAtAction(
                nameof(Login),
                ApiResponse<AuthResponseDto>.Ok(result.Tokens, "Inscription cabinet réussie"));
        }

        return result.FailureKind switch
        {
            AccountingFirmRegistrationFailureKind.Validation =>
                BadRequest(ApiResponse<AuthResponseDto>.Fail(
                    string.Join(", ", result.Errors))),
            AccountingFirmRegistrationFailureKind.DuplicateEmail =>
                BadRequest(ApiResponse<AuthResponseDto>.Fail(
                    string.Join(", ", result.Errors))),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<AuthResponseDto>.Fail(
                    result.Errors.FirstOrDefault() ?? "L'inscription du cabinet n'a pas pu être finalisée.",
                    $"FIRM-{result.CorrelationId}"))
        };
    }

    private async Task<UserDto> BuildUserProfileDtoAsync(ApplicationUser user, Tenant? tenant, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var snapshot = await _effectivePermissionService.GetUserAccessSnapshotAsync(user.Id, cancellationToken);
        return CreateUserDto(user, tenant, snapshot, roles);
    }

    private UserDto CreateUserDto(ApplicationUser user, Tenant? tenant, UserAccessSnapshot snapshot, IList<string> roles)
    {
        var roleName = roles.FirstOrDefault(r => !string.Equals(r, PlatformRoles.PlatformAdmin, StringComparison.Ordinal))
            ?? UserRole.Accountant.ToString();
        var roleEnum = Enum.TryParse<UserRole>(roleName, out var r) ? r : UserRole.Accountant;

        IReadOnlyList<int> enabledModuleIds;
        IReadOnlyList<string> effectivePermissions;
        if (tenant?.Kind == TenantKind.AccountingFirm)
        {
            enabledModuleIds = FirmGovernanceNativeAccess.BuildNativeFirmModuleIds(_firmGovernanceOptions);
            effectivePermissions = FirmGovernanceNativeAccess.AugmentNativeFirmPermissions(
                snapshot.EffectivePermissions.ToList(),
                roleEnum,
                _firmGovernanceOptions,
                _accountingFirmsOptions);
        }
        else
        {
            enabledModuleIds = snapshot.EnabledModules.Select(m => (int)m).ToList();
            effectivePermissions = snapshot.EffectivePermissions.ToList();
        }

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = roleEnum,
            RoleDisplay = roleEnum.ToDisplayString(),
            TenantId = user.TenantId,
            CompanyName = tenant?.CompanyName ?? "",
            TenantKind = tenant?.Kind ?? TenantKind.Company,
            AccessMode = "native",
            TwoFactorEnabled = user.TwoFactorEnabled,
            EnabledModuleIds = enabledModuleIds.ToList(),
            EffectivePermissions = effectivePermissions.ToList(),
            ProductOnboardingStatus = user.ProductOnboardingStatus,
            ProductOnboardingVersion = user.ProductOnboardingVersion,
            ProductOnboardingChecklist = ProductOnboardingUserDtoMapper.ChecklistOf(user),
            CompanySegment = tenant?.CompanySegment,
            BusinessDomain = tenant?.BusinessDomain
        };
    }

    /// <summary>
    /// Accept a client-portal invitation (anonymous). Sets password and issues a portal JWT.
    /// </summary>
    [HttpPost("portal/accept-invite")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptPortalInvite(
        [FromBody] AcceptPortalInviteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _clientPortalService.AcceptInviteAsync(request, cancellationToken);
        if (result.IsFailure)
            return ResultHttp.ToError(this, result.Error);

        return Ok(ApiResponse<AuthResponseDto>.Ok(result.Value, "Invitation acceptée"));
    }
}
