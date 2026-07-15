using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/accounting/fiscal-schedule/{entryId:guid}/attachments")]
[Authorize]
public sealed class FiscalScheduleAttachmentsController : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly IFiscalScheduleAttachmentService _attachments;

    public FiscalScheduleAttachmentsController(IFiscalScheduleAttachmentService attachments)
    {
        _attachments = attachments;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> List(Guid entryId, CancellationToken cancellationToken)
    {
        var r = await _attachments.ListAsync(entryId, cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<FiscalScheduleAttachmentDto>>.Ok(r.Value));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    [RequestSizeLimit(MaxUploadBytes + 512_000)]
    public async Task<IActionResult> Upload(Guid entryId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Fichier requis."));

        await using var stream = file.OpenReadStream();
        var r = await _attachments.UploadAsync(entryId, file.FileName, file.ContentType, stream, cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FiscalScheduleAttachmentDto>.Ok(r.Value, "Piece jointe ajoutee."));
    }

    [HttpGet("{attachmentId:guid}/download")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> Download(Guid entryId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var r = await _attachments.DownloadAsync(attachmentId, cancellationToken);
        if (r.IsFailure)
            return NotFound(ApiResponse<object>.Fail(r.Error.Description));
        return File(r.Value.Content, r.Value.ContentType, r.Value.FileName);
    }

    [HttpDelete("{attachmentId:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<IActionResult> Delete(Guid entryId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var r = await _attachments.DeleteAsync(entryId, attachmentId, cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true, "Piece jointe supprimee."));
    }
}
