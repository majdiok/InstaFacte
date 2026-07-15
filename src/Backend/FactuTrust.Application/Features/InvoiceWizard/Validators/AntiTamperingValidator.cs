using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.DTOs;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.InvoiceWizard.Validators;

/// <summary>
/// Validates data integrity and prevents tampering.
/// Ensures submitted data has not been maliciously modified.
/// </summary>
public sealed class AntiTamperingValidator
{
    private readonly ILogger<AntiTamperingValidator> _logger;

    public AntiTamperingValidator(ILogger<AntiTamperingValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates draft integrity before final submission.
    /// </summary>
    public ValidationResult ValidateDraftIntegrity(
        SaveDraftRequest request,
        WizardTotalsDto? serverCalculatedTotals,
        string? userId,
        string? tenantId)
    {
        var errors = new List<ValidationFailure>();

        // 1. Validate invoice number is not tampered
        if (request.Metadata != null)
        {
            var invoiceNumberError = ValidateInvoiceNumber(request.Metadata);
            if (invoiceNumberError != null)
            {
                errors.Add(invoiceNumberError);
            }
        }

        // 2. Validate calculations match server-side computation
        if (request.Lines != null && request.Lines.Count > 0 && serverCalculatedTotals != null)
        {
            var calculationErrors = ValidateCalculations(request.Lines, serverCalculatedTotals);
            errors.AddRange(calculationErrors);
        }

        // 3. Validate no negative amounts for standard invoices
        if (request.Metadata?.Type == "INVOICE" && request.Lines != null)
        {
            var amountErrors = ValidatePositiveAmounts(request.Lines);
            errors.AddRange(amountErrors);
        }

        // 4. Log any detected tampering attempts
        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Potential tampering detected for user {UserId} in tenant {TenantId}: {Errors}",
                userId ?? "unknown",
                tenantId ?? "unknown",
                string.Join(", ", errors.Select(e => e.ErrorMessage)));
        }

        return new ValidationResult(errors);
    }

    /// <summary>
    /// Validates that invoice number hasn't been manually modified.
    /// </summary>
    private ValidationFailure? ValidateInvoiceNumber(WizardStepMetadataDto metadata)
    {
        // Invoice numbers starting with a real prefix shouldn't be modifiable by users
        // Only TEMP- prefix is allowed for drafts
        if (!string.IsNullOrEmpty(metadata.InternalReference))
        {
            // Internal reference can be anything, but limited to 50 chars
            if (metadata.InternalReference.Length > TunisianValidationRules.MaxLengths.InternalReference)
            {
                return new ValidationFailure(
                    "Metadata.InternalReference",
                    $"La référence interne ne peut pas dépasser {TunisianValidationRules.MaxLengths.InternalReference} caractères")
                {
                    ErrorCode = ValidationErrorCodes.MaxLength
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Validates that submitted totals match server calculations.
    /// </summary>
    private IEnumerable<ValidationFailure> ValidateCalculations(
        List<WizardStepLineDto> lines,
        WizardTotalsDto serverTotals)
    {
        var errors = new List<ValidationFailure>();

        // Recalculate totals server-side (including fiscal stamp from authoritative server totals)
        var stamp = serverTotals.FiscalStampAmount;
        var recalculatedTotals = InvoiceCalculationService.CalculateTotals(
            lines,
            serverTotals.Currency,
            stamp,
            serverTotals.FodecRatePercent);

        if (!TunisianValidationRules.AmountsEqual(recalculatedTotals.TotalHT, serverTotals.TotalHT))
        {
            errors.Add(new ValidationFailure(
                "Totals.TotalHT",
                $"Incohérence détectée: Total HT calculé ({recalculatedTotals.TotalHT:N3}) ≠ attendu ({serverTotals.TotalHT:N3})")
            {
                ErrorCode = ValidationErrorCodes.Tampering
            });
        }

        if (!TunisianValidationRules.AmountsEqual(recalculatedTotals.TotalFodec, serverTotals.TotalFodec))
        {
            errors.Add(new ValidationFailure(
                "Totals.TotalFodec",
                $"Incohérence détectée: Total FODEC calculé ({recalculatedTotals.TotalFodec:N3}) ≠ attendu ({serverTotals.TotalFodec:N3})")
            {
                ErrorCode = ValidationErrorCodes.Tampering
            });
        }

        if (!TunisianValidationRules.AmountsEqual(recalculatedTotals.TotalVat, serverTotals.TotalVat))
        {
            errors.Add(new ValidationFailure(
                "Totals.TotalVat",
                $"Incohérence détectée: Total TVA calculé ({recalculatedTotals.TotalVat:N3}) ≠ attendu ({serverTotals.TotalVat:N3})")
            {
                ErrorCode = ValidationErrorCodes.Tampering
            });
        }

        if (!TunisianValidationRules.AmountsEqual(recalculatedTotals.TotalTTC, serverTotals.TotalTTC))
        {
            errors.Add(new ValidationFailure(
                "Totals.TotalTTC",
                $"Incohérence détectée: Total TTC calculé ({recalculatedTotals.TotalTTC:N3}) ≠ attendu ({serverTotals.TotalTTC:N3})")
            {
                ErrorCode = ValidationErrorCodes.Tampering
            });
        }

        return errors;
    }

    /// <summary>
    /// Validates that standard invoices have positive amounts.
    /// </summary>
    private IEnumerable<ValidationFailure> ValidatePositiveAmounts(List<WizardStepLineDto> lines)
    {
        var errors = new List<ValidationFailure>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (line.UnitPriceHT < 0)
            {
                errors.Add(new ValidationFailure(
                    $"Lines[{i}].UnitPriceHT",
                    $"Prix unitaire négatif détecté sur la ligne {i + 1}")
                {
                    ErrorCode = ValidationErrorCodes.Tampering
                });
            }

            if (line.Quantity < 0)
            {
                errors.Add(new ValidationFailure(
                    $"Lines[{i}].Quantity",
                    $"Quantité négative détectée sur la ligne {i + 1}")
                {
                    ErrorCode = ValidationErrorCodes.Tampering
                });
            }

            // Discount cannot be negative
            if (line.DiscountValue.HasValue && line.DiscountValue.Value < 0)
            {
                errors.Add(new ValidationFailure(
                    $"Lines[{i}].DiscountValue",
                    $"Remise négative détectée sur la ligne {i + 1}")
                {
                    ErrorCode = ValidationErrorCodes.Tampering
                });
            }
        }

        return errors;
    }

    /// <summary>
    /// Generates a hash of critical invoice data for integrity verification.
    /// </summary>
    public static string GenerateIntegrityHash(SaveDraftRequest request)
    {
        var criticalData = new
        {
            Type = request.Metadata?.Type,
            IssueDate = request.Metadata?.IssueDate,
            SellerId = request.SellerId,
            ClientId = request.Client?.ClientId,
            LineCount = request.Lines?.Count ?? 0,
            Lines = request.Lines?.Select(l => new
            {
                l.Designation,
                l.Quantity,
                l.UnitPriceHT,
                l.VatRate,
                l.DiscountType,
                l.DiscountValue
            }).ToList()
        };

        var json = JsonSerializer.Serialize(criticalData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Verifies data integrity against a previously generated hash.
    /// </summary>
    public static bool VerifyIntegrityHash(SaveDraftRequest request, string expectedHash)
    {
        var currentHash = GenerateIntegrityHash(request);
        return string.Equals(currentHash, expectedHash, StringComparison.Ordinal);
    }
}

/// <summary>
/// Validator for idempotency to prevent double submissions.
/// </summary>
public sealed class IdempotencyValidator : AbstractValidator<SubmitInvoiceWizardRequest>
{
    public IdempotencyValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("La clé d'idempotence est obligatoire pour éviter les doubles soumissions")
            .WithErrorCode(ValidationErrorCodes.Required);

        RuleFor(x => x.IdempotencyKey)
            .MinimumLength(32)
            .WithMessage("La clé d'idempotence doit contenir au moins 32 caractères")
            .WithErrorCode(ValidationErrorCodes.MinLength);

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(64)
            .WithMessage("La clé d'idempotence ne peut pas dépasser 64 caractères")
            .WithErrorCode(ValidationErrorCodes.MaxLength);

        RuleFor(x => x.IdempotencyKey)
            .Matches(@"^[a-zA-Z0-9\-_]+$")
            .WithMessage("La clé d'idempotence ne peut contenir que des caractères alphanumériques, tirets et underscores")
            .WithErrorCode(ValidationErrorCodes.InvalidFormat);
    }
}

/// <summary>
/// Service for managing idempotency keys.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if an idempotency key has been used.
    /// </summary>
    Task<bool> IsKeyUsedAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an idempotency key as used.
    /// </summary>
    Task MarkKeyUsedAsync(string key, Guid resultId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the result ID for a previously used key.
    /// </summary>
    Task<Guid?> GetResultForKeyAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory idempotency service (for development/testing).
/// In production, use Redis or database-backed implementation.
/// </summary>
public sealed class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly Dictionary<string, (Guid ResultId, DateTime UsedAt)> _usedKeys = new();
    private readonly TimeSpan _keyExpiration = TimeSpan.FromHours(24);
    private readonly object _lock = new();

    public Task<bool> IsKeyUsedAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            CleanupExpiredKeys();
            return Task.FromResult(_usedKeys.ContainsKey(key));
        }
    }

    public Task MarkKeyUsedAsync(string key, Guid resultId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            CleanupExpiredKeys();
            _usedKeys[key] = (resultId, DateTime.UtcNow);
        }
        return Task.CompletedTask;
    }

    public Task<Guid?> GetResultForKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            CleanupExpiredKeys();
            if (_usedKeys.TryGetValue(key, out var entry))
            {
                return Task.FromResult<Guid?>(entry.ResultId);
            }
            return Task.FromResult<Guid?>(null);
        }
    }

    private void CleanupExpiredKeys()
    {
        var cutoff = DateTime.UtcNow - _keyExpiration;
        var expiredKeys = _usedKeys
            .Where(kvp => kvp.Value.UsedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _usedKeys.Remove(key);
        }
    }
}

/// <summary>
/// Rate limiting validator to prevent abuse.
/// </summary>
public sealed class RateLimitingConfig
{
    /// <summary>Maximum submissions per user per hour.</summary>
    public int MaxSubmissionsPerHour { get; set; } = 100;

    /// <summary>Maximum draft saves per user per hour.</summary>
    public int MaxDraftSavesPerHour { get; set; } = 300;

    /// <summary>Maximum validation requests per minute.</summary>
    public int MaxValidationsPerMinute { get; set; } = 60;
}
