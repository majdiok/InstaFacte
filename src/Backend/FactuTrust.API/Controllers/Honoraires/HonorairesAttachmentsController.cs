using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Honoraires;

[ApiController]
[Route("api/honoraires/attachments")]
[Authorize]
public sealed class HonorairesAttachmentsController : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly IHonorairesBillingService _service;

    public HonorairesAttachmentsController(IHonorairesBillingService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HonorairesAttachmentListItemDto>>>> List(
        [FromQuery] HonorairesAttachmentDocumentKind kind,
        [FromQuery] Guid documentId,
        CancellationToken cancellationToken)
    {
        var items = await _service.ListAttachmentsAsync(kind, documentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HonorairesAttachmentListItemDto>>.Ok(items));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesUpdate)]
    [RequestSizeLimit(MaxUploadBytes + 512_000)]
    public async Task<ActionResult<ApiResponse<Guid>>> Upload(
        [FromQuery] HonorairesAttachmentDocumentKind kind,
        [FromQuery] Guid documentId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<Guid>.Fail("Fichier requis."));

        await using var stream = file.OpenReadStream();
        var result = await _service.AddAttachmentAsync(
            kind, documentId, file.FileName, file.ContentType, stream, file.Length, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Pièce jointe ajoutée"));
    }

    [HttpDelete("{attachmentId:guid}")]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAttachmentAsync(attachmentId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Pièce jointe supprimée"));
    }
}
