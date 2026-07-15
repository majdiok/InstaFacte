using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using QRCoder;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Lot B2 — Service 2FA TOTP basé sur Otp.NET (RFC 6238) + QRCoder + ASP.NET DataProtection
/// pour le chiffrement du secret. Voir <see cref="IPlatformMfaService"/> pour le contrat.
/// </summary>
public sealed class TotpPlatformMfaService : IPlatformMfaService
{
    private const string DataProtectorPurpose = "PlatformAdminMfa";
    private const int TotpDigits = 6;
    private const int TotpStepSeconds = 30;
    private const int RecoveryCodesCount = 10;
    private const string Issuer = "FactuTrust";

    private readonly MasterDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDataProtector _protector;

    public TotpPlatformMfaService(
        MasterDbContext db,
        UserManager<ApplicationUser> userManager,
        IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        _userManager = userManager;
        _protector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);
    }

    public async Task<MfaStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.PlatformAdminProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return new MfaStatusDto { IsEnabled = false, HasPendingSetup = false, RemainingRecoveryCodes = 0 };
        }

        var remaining = 0;
        if (!string.IsNullOrEmpty(profile.RecoveryCodesJson))
        {
            try
            {
                var hashes = JsonSerializer.Deserialize<List<string>>(profile.RecoveryCodesJson) ?? new();
                remaining = hashes.Count;
            }
            catch (JsonException) { /* malformed → 0 */ }
        }

        return new MfaStatusDto
        {
            IsEnabled = profile.IsMfaEnabled,
            HasPendingSetup = profile.MfaSecretEncrypted is not null && profile.MfaConfirmedAt is null,
            EnabledAt = profile.MfaConfirmedAt,
            IsLocked = profile.IsMfaLocked,
            LockoutUntil = profile.MfaLockoutUntil,
            RemainingRecoveryCodes = remaining
        };
    }

    public async Task<MfaSetupDto> StartSetupAsync(
        Guid userId,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.PlatformAdminProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = PlatformAdminProfile.CreateForUser(userId);
            _db.PlatformAdminProfiles.Add(profile);
        }

        var secretBytes = KeyGeneration.GenerateRandomKey(20); // 160 bits, recommandé par RFC 4226
        var base32 = Base32Encoding.ToString(secretBytes);
        var encrypted = _protector.Protect(base32);

        profile.BeginMfaSetup(encrypted);
        await _db.SaveChangesAsync(cancellationToken);

        var label = Uri.EscapeDataString($"{Issuer}:{userEmail}");
        var issuerEnc = Uri.EscapeDataString(Issuer);
        var otpAuthUri =
            $"otpauth://totp/{label}?secret={base32}&issuer={issuerEnc}&algorithm=SHA1&digits={TotpDigits}&period={TotpStepSeconds}";

        var qrCodeDataUri = GenerateQrCodePngDataUri(otpAuthUri);

        return new MfaSetupDto
        {
            SecretBase32 = base32,
            QrCodeDataUri = qrCodeDataUri,
            OtpAuthUri = otpAuthUri
        };
    }

    public async Task<Result<MfaConfirmDto>> ConfirmSetupAsync(
        Guid userId,
        string totpCode,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.PlatformAdminProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null || profile.MfaSecretEncrypted is null)
            return Result.Failure<MfaConfirmDto>(Error.Validation("Mfa.NoSetup", "Aucun setup 2FA en cours."));

        if (!TryDecrypt(profile.MfaSecretEncrypted, out var base32))
            return Result.Failure<MfaConfirmDto>(Error.Validation("Mfa.SecretCorrupt", "Le secret 2FA est corrompu, recommencez le setup."));

        if (!VerifyTotpInternal(base32!, totpCode))
            return Result.Failure<MfaConfirmDto>(Error.Validation("Mfa.InvalidCode", "Code invalide ou expiré."));

        // Génère 10 recovery codes en clair, persiste leurs hashes SHA-256 (codes longs aléatoires → SHA-256 suffit)
        var plainCodes = Enumerable.Range(0, RecoveryCodesCount).Select(_ => GenerateRecoveryCode()).ToList();
        var hashes = plainCodes.Select(HashRecoveryCode).ToList();
        profile.ConfirmMfaSetup(JsonSerializer.Serialize(hashes));

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new MfaConfirmDto
        {
            RecoveryCodes = plainCodes,
            EnabledAt = profile.MfaConfirmedAt!.Value
        });
    }

    public async Task<Result> VerifyAsync(Guid userId, string totpCode, CancellationToken cancellationToken = default)
    {
        var profile = await _db.PlatformAdminProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null || !profile.IsMfaEnabled)
            return Result.Failure(Error.Validation("Mfa.NotEnabled", "Le 2FA n'est pas activé pour cet utilisateur."));

        if (profile.IsMfaLocked)
            return Result.Failure(Error.Validation("Mfa.Locked", "Trop d'échecs. Réessayez plus tard."));

        if (!TryDecrypt(profile.MfaSecretEncrypted!, out var base32))
            return Result.Failure(Error.Validation("Mfa.SecretCorrupt", "Le secret 2FA est corrompu."));

        if (!VerifyTotpInternal(base32!, totpCode))
        {
            profile.RegisterFailedAttempt();
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.Validation("Mfa.InvalidCode", "Code invalide ou expiré."));
        }

        profile.ResetFailedAttempts();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> VerifyRecoveryCodeAsync(
        Guid userId,
        string recoveryCode,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.PlatformAdminProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null || !profile.IsMfaEnabled || string.IsNullOrEmpty(profile.RecoveryCodesJson))
            return Result.Failure(Error.Validation("Mfa.NotEnabled", "Le 2FA n'est pas activé pour cet utilisateur."));

        if (profile.IsMfaLocked)
            return Result.Failure(Error.Validation("Mfa.Locked", "Trop d'échecs. Réessayez plus tard."));

        var hashes = JsonSerializer.Deserialize<List<string>>(profile.RecoveryCodesJson) ?? new();
        var attemptHash = HashRecoveryCode(recoveryCode.Trim().ToUpperInvariant().Replace("-", ""));

        if (!hashes.Remove(attemptHash))
        {
            profile.RegisterFailedAttempt();
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.Validation("Mfa.InvalidCode", "Code de récupération invalide."));
        }

        profile.UpdateRecoveryCodes(JsonSerializer.Serialize(hashes));
        profile.ResetFailedAttempts();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DisableAsync(
        Guid userId,
        string password,
        string totpCode,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure(Error.NotFound("User", userId));

        if (!await _userManager.CheckPasswordAsync(user, password))
            return Result.Failure(Error.Validation("Mfa.WrongPassword", "Mot de passe incorrect."));

        var verifyResult = await VerifyAsync(userId, totpCode, cancellationToken);
        if (verifyResult.IsFailure)
            return verifyResult;

        var profile = await _db.PlatformAdminProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
            return Result.Success(); // déjà désactivé

        profile.DisableMfa();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ===== Helpers privés ===================================================

    private bool TryDecrypt(string encrypted, out string? plain)
    {
        try
        {
            plain = _protector.Unprotect(encrypted);
            return true;
        }
        catch
        {
            plain = null;
            return false;
        }
    }

    private static bool VerifyTotpInternal(string base32Secret, string code)
    {
        try
        {
            var bytes = Base32Encoding.ToBytes(base32Secret);
            var totp = new Totp(bytes, step: TotpStepSeconds, mode: OtpHashMode.Sha1, totpSize: TotpDigits);
            // Tolérance : ±1 step (≈ ±30 s) pour absorber les dérives d'horloge.
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateRecoveryCode()
    {
        // Format : XXXX-XXXX (10 caractères alphanumériques, pas de O/0/I/l ambiguïté)
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(10);
        var chars = new char[10];
        for (var i = 0; i < 10; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }
        return new string(chars, 0, 4) + "-" + new string(chars, 4, 6);
    }

    private static string HashRecoveryCode(string code)
    {
        var normalized = code.Trim().ToUpperInvariant().Replace("-", "");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string GenerateQrCodePngDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var pngBytes = qr.GetGraphic(pixelsPerModule: 8);
        return "data:image/png;base64," + Convert.ToBase64String(pngBytes);
    }
}
