using System.Text.Json;
using FluentValidation;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.API.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Transforms exceptions into standardized API responses with proper error codes.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validationException => HandleValidationException(validationException),
            UnauthorizedAccessException => HandleUnauthorizedException(),
            KeyNotFoundException keyNotFound => HandleNotFoundException(keyNotFound),
            InvalidOperationException invalidOp => HandleInvalidOperationException(invalidOp),
            SecurityException securityEx => HandleSecurityException(securityEx),
            ConflictException conflictEx => HandleConflictException(conflictEx),
            ComplianceException complianceEx => HandleComplianceException(complianceEx),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException concurrencyEx => HandleDbUpdateConcurrencyException(concurrencyEx),
            Microsoft.EntityFrameworkCore.DbUpdateException dbEx => HandleDbUpdateException(dbEx),
            _ => HandleUnknownException(exception)
        };

        // Log based on severity
        LogException(exception, statusCode);

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(response, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Handles FluentValidation exceptions with field-level error mapping.
    /// </summary>
    private (int StatusCode, ValidationErrorResponse) HandleValidationException(ValidationException exception)
    {
        var fieldErrors = new Dictionary<string, FieldError[]>();

        // Group errors by property name
        var groupedErrors = exception.Errors
            .GroupBy(e => e.PropertyName ?? "global")
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => new FieldError
                {
                    Code = e.ErrorCode ?? ValidationErrorCodes.InvalidFormat,
                    Message = e.ErrorMessage,
                    AttemptedValue = e.AttemptedValue,
                    Severity = e.Severity == Severity.Warning ? ErrorSeverity.Warning : ErrorSeverity.Error
                }).ToArray()
            );

        // Determine wizard step from field paths
        var wizardStep = WizardFieldMapping.GetFirstErrorStep(groupedErrors);

        var response = new ValidationErrorResponse
        {
            Code = ValidationErrorCodes.ValidationFailed,
            Message = "La validation a échoué. Veuillez corriger les erreurs indiquées.",
            FieldErrors = groupedErrors,
            WizardStep = wizardStep,
            CanProceed = !exception.Errors.Any(e => e.Severity == Severity.Error),
            ErrorCount = exception.Errors.Count(e => e.Severity == Severity.Error),
            WarningCount = exception.Errors.Count(e => e.Severity == Severity.Warning)
        };

        return (StatusCodes.Status400BadRequest, response);
    }

    private (int StatusCode, ValidationErrorResponse) HandleUnauthorizedException()
    {
        return (StatusCodes.Status401Unauthorized, new ValidationErrorResponse
        {
            Code = ValidationErrorCodes.Unauthorized,
            Message = "Accès non autorisé. Veuillez vous connecter.",
            GlobalErrors = new[] { "Authentification requise" },
            CanProceed = false,
            ErrorCount = 1
        });
    }

    private (int StatusCode, ValidationErrorResponse) HandleNotFoundException(KeyNotFoundException exception)
    {
        var translatedMessage = ErrorMessageTranslator.TranslateToFrench(exception.Message);
        return (StatusCodes.Status404NotFound, new ValidationErrorResponse
        {
            Code = ValidationErrorCodes.NotFound,
            Message = translatedMessage,
            GlobalErrors = new[] { translatedMessage },
            CanProceed = false,
            ErrorCount = 1
        });
    }

    private (int StatusCode, ValidationErrorResponse) HandleInvalidOperationException(InvalidOperationException exception)
    {
        var translatedMessage = ErrorMessageTranslator.TranslateToFrench(exception.Message);
        return (StatusCodes.Status400BadRequest, new ValidationErrorResponse
        {
            Code = ValidationErrorCodes.InvalidState,
            Message = translatedMessage,
            GlobalErrors = new[] { translatedMessage },
            CanProceed = false,
            ErrorCount = 1
        });
    }

    private (int StatusCode, ValidationErrorResponse) HandleSecurityException(SecurityException exception)
    {
        var translatedMessage = ErrorMessageTranslator.TranslateToFrench(exception.Message);
        return (StatusCodes.Status403Forbidden, new ValidationErrorResponse
        {
            Code = ValidationErrorCodes.SecurityError,
            Message = "Opération non autorisée pour des raisons de sécurité.",
            GlobalErrors = new[] { translatedMessage },
            CanProceed = false,
            ErrorCount = 1
        });
    }

    private (int StatusCode, ValidationErrorResponse) HandleConflictException(ConflictException exception)
    {
        var translatedMessage = ErrorMessageTranslator.TranslateToFrench(exception.Message);
        return (StatusCodes.Status409Conflict, new ValidationErrorResponse
        {
            Code = exception.Code ?? ValidationErrorCodes.AlreadyExists,
            Message = translatedMessage,
            GlobalErrors = new[] { translatedMessage },
            CanProceed = false,
            ErrorCount = 1
        });
    }

    private (int StatusCode, ValidationErrorResponse) HandleComplianceException(ComplianceException exception)
    {
        var translatedMessage = ErrorMessageTranslator.TranslateToFrench(exception.Message);
        return (StatusCodes.Status400BadRequest, new ValidationErrorResponse
        {
            Code = ValidationErrorCodes.ComplianceError,
            Message = translatedMessage,
            FieldErrors = exception.FieldErrors ?? new Dictionary<string, FieldError[]>(),
            WizardStep = exception.WizardStep,
            CanProceed = exception.CanProceed,
            ErrorCount = exception.ErrorCount,
            WarningCount = exception.WarningCount
        });
    }

    private (int StatusCode, ValidationErrorResponse) HandleDbUpdateConcurrencyException(
        Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException exception)
    {
        _logger.LogWarning(exception, "Database concurrency conflict during save");

        var message =
            "Les données ont été modifiées entre-temps par un autre processus. Actualisez la page et réessayez.";

        if (_environment.IsDevelopment())
        {
            var detail = exception.GetBaseException().Message;
            message += $" (Détail: {detail})";
        }

        return (StatusCodes.Status409Conflict, new ValidationErrorResponse
        {
            Code = ValidationErrorCodes.ConcurrencyConflict,
            Message = message,
            GlobalErrors = new[] { message },
            CanProceed = false,
            ErrorCount = 1
        });
    }

    private (int StatusCode, ValidationErrorResponse) HandleDbUpdateException(Microsoft.EntityFrameworkCore.DbUpdateException exception)
    {
        var errorMessage = "Erreur lors de l'enregistrement des données.";

        var sqlException = FindSqlException(exception);
        if (sqlException != null)
        {
            switch (sqlException.Number)
            {
                case 207:
                case 208:
                    if (IsStockSchemaMissing(sqlException.Message))
                    {
                        errorMessage = "Le schéma stock n'est pas migré pour cette entreprise. Lancez la migration.";
                    }
                    else
                    {
                        errorMessage = "Le schéma de base de données n'est pas à jour pour cette entreprise. Lancez la migration.";
                    }
                    break;
                case 2601:
                case 2627:
                    if (IsStockItemUniqueViolation(sqlException.Message))
                    {
                        errorMessage = "Contrainte d'unicité violée. Un stock existe déjà pour ce produit et cet entrepôt.";
                    }
                    else
                    {
                        errorMessage = "Une contrainte d'unicité a été violée. Cette valeur existe déjà.";
                        if (_environment.IsDevelopment())
                        {
                            errorMessage += $" (Détail: {sqlException.Message})";
                        }
                    }
                    break;
                default:
                    if (_environment.IsDevelopment())
                    {
                        errorMessage = $"Erreur de base de données: {sqlException.Message}";
                    }
                    break;
            }
        }
        else
        {
            var baseMessage = exception.GetBaseException().Message;

            if (IsStockSchemaMissing(baseMessage))
            {
                errorMessage = "Le schéma stock n'est pas migré pour cette entreprise. Lancez la migration.";
            }
            else if (IsStockItemUniqueViolation(baseMessage))
            {
                errorMessage = "Contrainte d'unicité violée. Un stock existe déjà pour ce produit et cet entrepôt.";
            }
            else if (baseMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                     baseMessage.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Une contrainte d'unicité a été violée. Cette valeur existe déjà.";
                if (_environment.IsDevelopment())
                {
                    errorMessage += $" (Détail: {baseMessage})";
                }
            }
            else if (baseMessage.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Une erreur de cohérence des données s'est produite. Un enregistrement avec cet identifiant existe déjà.";
            }
            else if (baseMessage.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Une référence invalide a été détectée. Veuillez vérifier que toutes les références sont valides.";
            }
            else if (_environment.IsDevelopment())
            {
                errorMessage = $"Erreur de base de données: {baseMessage}";
            }
        }

        return (StatusCodes.Status400BadRequest, new ValidationErrorResponse
        {
            Code = ValidationErrorCodes.InvalidState,
            Message = errorMessage,
            GlobalErrors = new[] { errorMessage },
            CanProceed = false,
            ErrorCount = 1
        });
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            if (current is SqlException sqlException)
                return sqlException;

            current = current.InnerException;
        }

        return null;
    }

    private static bool IsStockSchemaMissing(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase) &&
               (message.Contains("StockItems", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("StockMovements", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Warehouses", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStockItemUniqueViolation(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        // Seulement si le message référence explicitement l'index ou la table StockItems
        return message.Contains("IX_StockItems_ProductId_WarehouseId", StringComparison.OrdinalIgnoreCase) ||
               (message.Contains("StockItems", StringComparison.OrdinalIgnoreCase) &&
                (message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)));
    }

    private (int StatusCode, ValidationErrorResponse) HandleUnknownException(Exception exception)
    {
        // Log full exception details for debugging
        var exceptionDetails = _environment.IsDevelopment()
            ? $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}"
            : null;

        var message = _environment.IsDevelopment()
            ? ErrorMessageTranslator.TranslateToFrench(exception.Message)
            : "Une erreur interne s'est produite. Veuillez réessayer ou contacter le support.";

        // Include inner exception message if available (useful for debugging)
        if (_environment.IsDevelopment() && exception.InnerException != null)
        {
            message += $" (Erreur interne: {exception.InnerException.Message})";
        }

        return (StatusCodes.Status500InternalServerError, new ValidationErrorResponse
        {
            Code = "INTERNAL_ERROR",
            Message = message,
            GlobalErrors = new[] { message },
            CanProceed = false,
            ErrorCount = 1
        });
    }

    private void LogException(Exception exception, int statusCode)
    {
        var logLevel = statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(
            logLevel,
            exception,
            "HTTP {StatusCode} - {ExceptionType}: {Message}",
            statusCode,
            exception.GetType().Name,
            exception.Message);
    }
}

/// <summary>
/// Custom exception for security-related issues.
/// </summary>
public class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Custom exception for conflict scenarios (duplicate submission, etc.).
/// </summary>
public class ConflictException : Exception
{
    public string? Code { get; }

    public ConflictException(string message, string? code = null) : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// Custom exception for compliance validation failures.
/// </summary>
public class ComplianceException : Exception
{
    public Dictionary<string, FieldError[]>? FieldErrors { get; }
    public int? WizardStep { get; }
    public bool CanProceed { get; }
    public int ErrorCount { get; }
    public int WarningCount { get; }

    public ComplianceException(
        string message,
        Dictionary<string, FieldError[]>? fieldErrors = null,
        int? wizardStep = null,
        bool canProceed = false,
        int errorCount = 1,
        int warningCount = 0) : base(message)
    {
        FieldErrors = fieldErrors;
        WizardStep = wizardStep;
        CanProceed = canProceed;
        ErrorCount = errorCount;
        WarningCount = warningCount;
    }

    /// <summary>
    /// Creates a ComplianceException from a validation result.
    /// </summary>
    public static ComplianceException FromValidationResult(WizardValidationResultDto result)
    {
        var fieldErrors = result.Checks
            .Where(c => c.Status != "VALID" && c.Field != null)
            .GroupBy(c => c.Field!)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => new FieldError
                {
                    Code = c.Status == "ERROR" ? ValidationErrorCodes.ComplianceError : "WARNING",
                    Message = c.Description,
                    Severity = c.Status == "ERROR" ? ErrorSeverity.Error : ErrorSeverity.Warning
                }).ToArray()
            );

        // Determine first error step
        int? wizardStep = null;
        foreach (var check in result.Checks.Where(c => c.Status == "ERROR" && c.Field != null))
        {
            var step = WizardFieldMapping.GetStepForField(check.Field!);
            if (step.HasValue && (!wizardStep.HasValue || step.Value < wizardStep.Value))
            {
                wizardStep = step.Value;
            }
        }

        return new ComplianceException(
            "La facture ne respecte pas les exigences de conformité fiscale tunisienne.",
            fieldErrors,
            wizardStep,
            result.CanProceed,
            result.ErrorCount,
            result.WarningCount);
    }
}
