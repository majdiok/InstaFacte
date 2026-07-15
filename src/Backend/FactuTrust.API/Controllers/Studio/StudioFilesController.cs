using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Uploads files for Studio Attachment / Signature fields. Returns the stored file's relative URL,
/// which the record then stores in its JSON. Gated by the runtime write permission; tenant- and
/// entity-scoped on disk. Max 5 MB; images and PDF only.
/// </summary>
[ApiController]
[Route("api/studio/records/{entityKey}/files")]
[Authorize(Policy = PermissionPolicies.CustomRecordsWrite)]
public sealed class StudioFilesController : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    private readonly IStudioFileStorageService _storage;
    private readonly ITenantContext _tenantContext;

    public StudioFilesController(IStudioFileStorageService storage, ITenantContext tenantContext)
    {
        _storage = storage;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes + 4096)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Upload(string entityKey, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is null)
            return BadRequest(ApiResponse<string>.Fail("Contexte tenant manquant."));
        if (!StudioKey.IsValidShape(entityKey))
            return BadRequest(ApiResponse<string>.Fail("Clé de table invalide."));
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("Aucun fichier fourni."));
        if (file.Length > MaxUploadBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ApiResponse<string>.Fail("Le fichier ne doit pas dépasser 5 Mo."));

        try
        {
            await using var stream = file.OpenReadStream();
            var relativeUrl = await _storage.SaveAsync(
                _tenantContext.TenantId.Value, entityKey, stream, file.ContentType ?? string.Empty, cancellationToken);
            return Ok(ApiResponse<string>.Ok(relativeUrl, "Fichier enregistré."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
