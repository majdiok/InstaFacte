using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/bank-reconciliation")]
[Authorize]
public sealed class BankReconciliationController : ControllerBase
{
    private readonly IBankReconciliationService _reconciliationService;

    public BankReconciliationController(IBankReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    [HttpPost("statements")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    [ProducesResponseType(typeof(ApiResponse<BankStatementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportStatement(
        [FromBody] ImportBankStatementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.ImportStatementAsync(request, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<BankStatementDto>.Ok(result.Value, "Relevé bancaire importé."));
    }

    /// <summary>
    /// Analyse un fichier de relevé bancaire (CSV/Excel) sans rien persister : lignes exploitables
    /// + anomalies. L'import définitif passe ensuite par <c>POST statements</c> (même transport
    /// multipart que la reprise de dossier comptable).
    /// </summary>
    [HttpPost("statements/import-file")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    [RequestSizeLimit(25_000_000)]
    [ProducesResponseType(typeof(ApiResponse<BankStatementFilePreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewStatementFile(
        [FromForm] IFormFile file,
        [FromForm] BankStatementFileFormat format,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Fichier requis."));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);

        var result = await _reconciliationService.PreviewStatementFileAsync(
            ms.ToArray(),
            format,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<BankStatementFilePreviewDto>.Ok(result.Value));
    }

    [HttpGet("statements/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    [ProducesResponseType(typeof(ApiResponse<BankStatementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatement(Guid id, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.GetStatementAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<BankStatementDto>.Ok(result.Value));
    }

    [HttpGet("statements")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BankStatementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatements(
        [FromQuery] string? accountNumber,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.GetStatementsAsync(accountNumber, from, to, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<BankStatementDto>>.Ok(result.Value));
    }

    [HttpPost("reconcile")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReconcileLine(
        [FromBody] ReconcileLineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.ReconcileLineAsync(request, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true, "Ligne rapprochée."));
    }

    [HttpPost("unreconcile/{lineId:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnreconcileLine(Guid lineId, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.UnreconcileLineAsync(lineId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true, "Rapprochement annulé."));
    }

    /// <summary>Association automatique : propositions par ligne + récapitulatif (ne persiste rien).</summary>
    [HttpPost("statements/{id:guid}/auto-associate")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    [ProducesResponseType(typeof(ApiResponse<AutoAssociationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AutoAssociate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.AutoAssociateAsync(id, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<AutoAssociationResultDto>.Ok(result.Value));
    }

    /// <summary>Rapproche en lot les paires confirmées (ligne de relevé ↔ ligne d'écriture).</summary>
    [HttpPost("statements/{id:guid}/apply-associations")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    [ProducesResponseType(typeof(ApiResponse<ApplyAssociationsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApplyAssociations(Guid id, [FromBody] ApplyAssociationsRequest request, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.ApplyAssociationsAsync(id, request, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<ApplyAssociationsResultDto>.Ok(result.Value, $"{result.Value.AppliedCount} rapprochement(s) appliqué(s)."));
    }

    /// <summary>Comptabilise une ligne non rapprochée : écriture 512 ↔ contrepartie + rapprochement.</summary>
    [HttpPost("statements/{id:guid}/lines/{lineId:guid}/create-entry")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEntryForLine(Guid id, Guid lineId, [FromBody] CreateEntryForLineRequest request, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.CreateEntryForLineAsync(lineId, request, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true, "Écriture comptabilisée."));
    }
}
