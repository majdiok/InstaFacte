using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
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
        LogCompanyRegistrationStep("Validation", validationSw.ElapsedMilliseconds, null, correlationId);

        var emailCheckSw = Stopwatch.StartNew();
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        LogCompanyRegistrationStep("EmailPrecheck", emailCheckSw.ElapsedMilliseconds, null, correlationId);
        if (existingUser is not null)
            return BadRequest(ApiResponse<AuthResponseDto>.Fail("Un compte existe déjà avec cette adresse e-mail."));

        string? provisionedDatabaseName = null;
        Guid? tenantId = null;

        await using var transaction = await _masterContext.Database.BeginTransactionAsync(cancellationToken);

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

            var tenant = tenantResult.Value;
            if (tenant.Id == Guid.Empty)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(ApiResponse<AuthResponseDto>.Fail("Erreur interne : identifiant entreprise invalide."));
            }

            tenantId = tenant.Id;
            provisionedDatabaseName = tenant.DatabaseName;

            var masterSw = Stopwatch.StartNew();
            _masterContext.Tenants.Add(tenant);

            var subscription = Subscription.CreateFree(tenant.Id);
            _masterContext.Subscriptions.Add(subscription);
            await _masterContext.SaveChangesAsync(cancellationToken);
            LogCompanyRegistrationStep("MasterEntities", masterSw.ElapsedMilliseconds, tenant.Id, correlationId);

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                TenantId = tenant.Id,
                EmailConfirmed = true // Set to false and require confirmation in production
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

            var provisionSw = Stopwatch.StartNew();
            await _tenantService.CreateTenantDatabaseAsync(tenant.Id, tenant.DatabaseName, dto.WarehouseName, cancellationToken);
            LogCompanyRegistrationStep("DatabaseProvision", provisionSw.ElapsedMilliseconds, tenant.Id, correlationId);

            await _masterContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var tokenSw = Stopwatch.StartNew();
            var tokens = await _tokenService.GenerateTokensAsync(user.Id, tenant.Id, cancellationToken: cancellationToken);
            LogCompanyRegistrationStep("TokenGeneration", tokenSw.ElapsedMilliseconds, tenant.Id, correlationId);

            totalSw.Stop();
            LogCompanyRegistrationStep("Total", totalSw.ElapsedMilliseconds, tenant.Id, correlationId);

            _logger.LogInformation(
                "User {Email} registered with tenant {TenantId}. CorrelationId={CorrelationId} DurationMs={DurationMs}",
                dto.Email,
                tenant.Id,
                correlationId,
                totalSw.ElapsedMilliseconds);

            return CreatedAtAction(nameof(Login), ApiResponse<AuthResponseDto>.Ok(tokens, "Inscription réussie"));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            if (!string.IsNullOrEmpty(provisionedDatabaseName))
                await _tenantService.TryDropDatabaseAsync(provisionedDatabaseName, cancellationToken);

            _logger.LogError(
                ex,
                "Registration failed for {Email}. CorrelationId: {CorrelationId}. TenantId: {TenantId}. Detail: {Detail}",
                dto.Email,
                correlationId,
                tenantId,
                ex.InnerException?.Message ?? ex.Message);

            const string userMessage =
                "L'inscription n'a pas pu être finalisée. Réessayez plus tard ou contactez le support.";
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<AuthResponseDto>.Fail(userMessage, $"REG-{correlationId}"));
        }
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
            _logger.LogWarning("Login failed for {Email}: user not found or inactive", dto.Email);
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Email ou mot de passe incorrect"));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Email} is locked out", dto.Email);
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Compte verrouillé. Réessayez dans 15 minutes."));
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed for {Email}: invalid password", dto.Email);
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Email ou mot de passe incorrect"));
        }

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
            _logger.LogWarning("User {Email} has no company (TenantId empty)", user.Email);
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Aucune entreprise associée à ce compte. Veuillez contacter l'administrateur."));
        }

        var tenant = await _masterContext.Tenants.FindAsync(new object[] { user.TenantId }, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Entreprise inactive ou introuvable"));
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var tokens = await _tokenService.GenerateTokensAsync(user.Id, tenant.Id, cancellationToken: cancellationToken);

        _logger.LogInformation("User {Email} logged in successfully", dto.Email);

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

                    _logger.LogInformation("Password reset email queued for {Email}", email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset email for {Email}", email);
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

        _logger.LogInformation("Password reset successful for {Email}", dto.Email);

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
            ProductOnboardingChecklist = ProductOnboardingUserDtoMapper.ChecklistOf(user)
        };
    }

}
