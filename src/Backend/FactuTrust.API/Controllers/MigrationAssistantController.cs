using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Migration.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Migration assistée par IA (N1) : analyse de format, mapping de colonnes, mapping de comptes
/// et détection de doublons. Ces endpoints ne font que SUGGÉRER — l'import effectif passe
/// ensuite par <c>reference-import/preview|commit</c>, inchangés.
/// Feature flag : <c>MigrationAi:Enabled</c> (défaut désactivé → 404).
/// </summary>
[ApiController]
[Route("api/accounting/migration")]
[Authorize]
public sealed class MigrationAssistantController : ControllerBase
{
    private const long MaxFileBytes = 25_000_000;

    private readonly IMediator _mediator;
    private readonly MigrationAiSettings _settings;

    public MigrationAssistantController(IMediator mediator, IOptions<MigrationAiSettings> settings)
    {
        _mediator = mediator;
        _settings = settings.Value;
    }

    /// <summary>Analyse un fichier source : progiciel détecté, format, cible probable.</summary>
    [HttpPost("analyze")]
    [Authorize(Policy = PermissionPolicies.AccountingImport)]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> Analyze([FromForm] MigrationFileFormRequest request, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) return NotFound();
        if (request.File is null || request.File.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Fichier requis."));

        var content = await ReadFileAsync(request.File, cancellationToken);
        var r = await _mediator.Send(new AnalyzeMigrationSourceCommand(request.File.FileName, content), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<MigrationAnalysisDto>.Ok(r.Value));
    }

    /// <summary>Suggestion de correspondance colonnes source → schéma canonique de la cible.</summary>
    [HttpPost("suggest-column-mapping")]
    [Authorize(Policy = PermissionPolicies.AccountingImport)]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> SuggestColumnMapping(
        [FromForm] MigrationTargetedFileFormRequest request, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) return NotFound();
        if (request.File is null || request.File.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Fichier requis."));

        var content = await ReadFileAsync(request.File, cancellationToken);
        var format = FactuTrust.Infrastructure.Services.Migration.MigrationFileInspector.DetectFormat(request.File.FileName);
        var r = await _mediator.Send(
            new SuggestMigrationColumnMappingCommand(content, format, request.Target), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<ColumnMappingSuggestionDto>.Ok(r.Value));
    }

    /// <summary>
    /// Suggestion de correspondance comptes source → plan local. Le mapping de colonnes validé
    /// (JSON : en-tête source → canonique) peut être joint dans le champ <c>columnMapping</c>.
    /// </summary>
    [HttpPost("suggest-account-mapping")]
    [Authorize(Policy = PermissionPolicies.AccountingImport)]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> SuggestAccountMapping(
        [FromForm] MigrationAccountMappingFormRequest request, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) return NotFound();
        if (request.File is null || request.File.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Fichier requis."));

        Dictionary<string, string>? columnMapping = null;
        if (!string.IsNullOrWhiteSpace(request.ColumnMapping))
        {
            try
            {
                columnMapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.ColumnMapping);
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(ApiResponse<object>.Fail("columnMapping : JSON invalide (objet en-tête source → colonne canonique attendu)."));
            }
        }

        var content = await ReadFileAsync(request.File, cancellationToken);
        var format = FactuTrust.Infrastructure.Services.Migration.MigrationFileInspector.DetectFormat(request.File.FileName);
        var r = await _mediator.Send(
            new SuggestMigrationAccountMappingCommand(content, format, columnMapping), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountMappingSuggestionDto>.Ok(r.Value));
    }

    /// <summary>Détection de doublons de tiers (fichier ↔ référentiel, et interne au fichier).</summary>
    [HttpPost("detect-duplicates")]
    [Authorize(Policy = PermissionPolicies.AccountingImport)]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> DetectDuplicates([FromForm] MigrationFileFormRequest request, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) return NotFound();
        if (request.File is null || request.File.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Fichier requis."));

        var content = await ReadFileAsync(request.File, cancellationToken);
        var format = FactuTrust.Infrastructure.Services.Migration.MigrationFileInspector.DetectFormat(request.File.FileName);
        var r = await _mediator.Send(new DetectMigrationDuplicatesCommand(content, format), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<ThirdPartyDuplicateDetectionDto>.Ok(r.Value));
    }

    /// <summary>
    /// Réécrit le fichier source en CSV à en-têtes canoniques selon le mapping validé
    /// (champ <c>columnMapping</c> : JSON en-tête source → canonique). Le fichier transformé
    /// est destiné aux endpoints <c>reference-import/preview|commit</c>, inchangés.
    /// </summary>
    [HttpPost("transform")]
    [Authorize(Policy = PermissionPolicies.AccountingImport)]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> Transform(
        [FromForm] MigrationTransformFormRequest request, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled) return NotFound();
        if (request.File is null || request.File.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Fichier requis."));
        if (string.IsNullOrWhiteSpace(request.ColumnMapping))
            return BadRequest(ApiResponse<object>.Fail("columnMapping requis (JSON en-tête source → colonne canonique)."));

        Dictionary<string, string>? columnMapping;
        try
        {
            columnMapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.ColumnMapping);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(ApiResponse<object>.Fail("columnMapping : JSON invalide."));
        }
        if (columnMapping is null || columnMapping.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("columnMapping vide."));

        var content = await ReadFileAsync(request.File, cancellationToken);
        var format = FactuTrust.Infrastructure.Services.Migration.MigrationFileInspector.DetectFormat(request.File.FileName);
        var r = await _mediator.Send(
            new ApplyMigrationColumnMappingCommand(content, format, request.Target, columnMapping), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return File(r.Value, "text/csv", $"{Path.GetFileNameWithoutExtension(request.File.FileName)}-canonique.csv");
    }

    private static async Task<byte[]> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    /// <summary>Payload multipart minimal (fichier seul).</summary>
    public class MigrationFileFormRequest
    {
        public IFormFile File { get; init; } = null!;
    }

    /// <summary>Payload multipart avec cible d'import.</summary>
    public class MigrationTargetedFileFormRequest
    {
        public IFormFile File { get; init; } = null!;
        public ReferenceImportTarget Target { get; init; }
    }

    /// <summary>Payload multipart du mapping de comptes (mapping de colonnes validé, en JSON).</summary>
    public class MigrationAccountMappingFormRequest
    {
        public IFormFile File { get; init; } = null!;
        public string? ColumnMapping { get; init; }
    }

    /// <summary>Payload multipart de la transformation (cible + mapping de colonnes validé, en JSON).</summary>
    public class MigrationTransformFormRequest
    {
        public IFormFile File { get; init; } = null!;
        public ReferenceImportTarget Target { get; init; }
        public string? ColumnMapping { get; init; }
    }
}
