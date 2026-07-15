using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Runs <see cref="OpenXmlValidator"/> on the generated presentation before the bytes leave
/// the generator. PowerPoint Desktop is much stricter than the OpenXml SDK at load time —
/// validating against the SDK schema catches structural issues (missing parts, broken
/// relationships, unknown elements) that would otherwise surface as the dreaded
/// "PowerPoint a détecté un problème dans le contenu" repair dialog.
/// </summary>
public static class PowerPointValidationGate
{
    public static void ValidateIfEnabled(
        PresentationDocument document,
        PowerPointValidationOptions options,
        ILogger logger)
    {
        if (!options.Enabled)
            return;

        var validator = new OpenXmlValidator(FileFormatVersions.Office2019);
        var issues = validator
            .Validate(document)
            .Where(IsStructural)
            .Take(options.MaxIssuesLogged + 1)
            .ToList();

        if (issues.Count == 0)
            return;

        foreach (var issue in issues.Take(options.MaxIssuesLogged))
        {
            logger.LogWarning(
                "PowerPoint export validation issue: Id={Id} Type={Type} Path={Path} Description={Description}",
                issue.Id,
                issue.ErrorType,
                issue.Path?.XPath,
                Sanitize(issue.Description));
        }

        if (options.ThrowOnError)
        {
            var first = issues[0];
            throw new InvalidPresentationException(
                $"OpenXml validation failed: {first.Id} at {first.Path?.XPath}. {Sanitize(first.Description)}");
        }
    }

    private static bool IsStructural(ValidationErrorInfo info) =>
        info.ErrorType is ValidationErrorType.Schema
            or ValidationErrorType.Package
            or ValidationErrorType.Semantic;

    private static string? Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return text.Length <= 200 ? text : text[..200] + "…";
    }
}

/// <summary>
/// Configuration for <see cref="PowerPointValidationGate"/>. Bound from the
/// <c>AiExport:Validation</c> section of <c>appsettings.json</c>.
/// </summary>
public sealed class PowerPointValidationOptions
{
    /// <summary>Master switch — when false the validator is bypassed entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When true, the first structural error causes <see cref="InvalidPresentationException"/> to be thrown.</summary>
    public bool ThrowOnError { get; set; }

    /// <summary>Cap on the number of issues logged per export to avoid flooding the log pipeline.</summary>
    public int MaxIssuesLogged { get; set; } = 10;

    public static PowerPointValidationOptions Default => new();

    public static PowerPointValidationOptions StrictForTests => new()
    {
        Enabled = true,
        ThrowOnError = true,
        MaxIssuesLogged = 100
    };
}

/// <summary>
/// Thrown when <see cref="PowerPointValidationGate"/> detects a structural issue and is configured
/// to fail-fast. Production deployments default to log-only mode and never raise this.
/// </summary>
public sealed class InvalidPresentationException : Exception
{
    public InvalidPresentationException(string message) : base(message)
    {
    }
}
