using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

public enum AccountingFirmRegistrationFailureKind
{
    Validation,
    DuplicateEmail,
    Internal
}

public sealed class AccountingFirmRegistrationResult
{
    public bool Success { get; init; }
    public AuthResponseDto? Tokens { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public AccountingFirmRegistrationFailureKind? FailureKind { get; init; }
    public string? CorrelationId { get; init; }
    public long TotalDurationMs { get; init; }

    public static AccountingFirmRegistrationResult ValidationFailure(params string[] errors) =>
        new() { Success = false, FailureKind = AccountingFirmRegistrationFailureKind.Validation, Errors = errors };

    public static AccountingFirmRegistrationResult DuplicateEmailFailure() =>
        new()
        {
            Success = false,
            FailureKind = AccountingFirmRegistrationFailureKind.DuplicateEmail,
            Errors = new[] { "Un compte existe déjà avec cette adresse e-mail." }
        };

    public static AccountingFirmRegistrationResult InternalFailure(string correlationId) =>
        new()
        {
            Success = false,
            FailureKind = AccountingFirmRegistrationFailureKind.Internal,
            CorrelationId = correlationId,
            Errors = new[] { "L'inscription du cabinet n'a pas pu être finalisée." }
        };

    public static AccountingFirmRegistrationResult Ok(AuthResponseDto tokens, long totalDurationMs) =>
        new() { Success = true, Tokens = tokens, TotalDurationMs = totalDurationMs };
}

public interface IAccountingFirmRegistrationService
{
    Task<AccountingFirmRegistrationResult> RegisterAsync(
        RegisterAccountingFirmDto dto,
        CancellationToken cancellationToken = default);
}
