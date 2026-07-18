using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Uploads and serves files for Studio Attachment / Signature fields. Upload returns the stored file's
/// relative URL, which the record then stores in its JSON; download resolves the file from the
/// authenticated tenant's folder only (these files are excluded from static serving — Program.cs).
/// Upload is gated by the runtime write permission, download by the read permission. Max 5 MB;
/// images and PDF only.
/// </summary>
[ApiController]
[Route("api/studio/records/{entityKey}/files")]
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
    [Authorize(Policy = PermissionPolicies.CustomRecordsWrite)]
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

    /// <summary>
    /// Authenticated download of a previously uploaded Studio file. The file is resolved under the
    /// CURRENT tenant's folder only (tenant id from the auth context, never from the URL), so a foreign
    /// tenant's file name simply yields 404.
    /// </summary>
    [HttpGet("{fileName}")]
    [Authorize(Policy = PermissionPolicies.CustomRecordsRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Download(string entityKey, string fileName)
    {
        if (_tenantContext.TenantId is null)
            return NotFound();

        var file = _storage.Resolve(_tenantContext.TenantId.Value, entityKey, fileName);
        if (file is null)
            return NotFound();

        // File names are immutable GUIDs (replaced, never rewritten): private caching is safe.
        Response.Headers.CacheControl = "private, max-age=3600";
        return PhysicalFile(file.FullPath, file.ContentType);
    }
}
