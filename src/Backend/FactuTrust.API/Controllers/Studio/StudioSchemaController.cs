using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// READ-ONLY introspection of the tenant database for the view designer: list tables/columns and
/// preview rows. Design-time only (<c>studio:design_forms</c>); the provider enforces the denylist,
/// identifier validation, and SELECT-only execution.
/// </summary>
[ApiController]
[Route("api/studio/schema")]
[Authorize(Policy = PermissionPolicies.StudioDesignForms)]
public sealed class StudioSchemaController : ControllerBase
{
    private readonly ISqlSchemaProvider _schema;
    private readonly ISqlViewResultEnricher _enricher;
    private readonly ICurrentUser _currentUser;

    public StudioSchemaController(ISqlSchemaProvider schema, ISqlViewResultEnricher enricher, ICurrentUser currentUser)
    {
        _schema = schema;
        _enricher = enricher;
        _currentUser = currentUser;
    }

    [HttpGet("tables")]
    public async Task<IActionResult> Tables(CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Unauthorized(ApiResponse<object>.Fail("Aucun tenant authentifié.", "Unauthorized"));
        var tables = await _schema.ListTablesAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SqlTableInfo>>.Ok(tables));
    }

    [HttpGet("tables/{table}/columns")]
    public async Task<IActionResult> Columns(string table, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Unauthorized(ApiResponse<object>.Fail("Aucun tenant authentifié.", "Unauthorized"));
        var columns = await _schema.ListColumnsAsync(tenantId, table, cancellationToken);
        return columns is null
            ? NotFound(ApiResponse<object>.Fail("Table non autorisée ou introuvable.", "Forbidden"))
            : Ok(ApiResponse<IReadOnlyList<SqlColumnInfo>>.Ok(columns));
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] SqlPreviewRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Unauthorized(ApiResponse<object>.Fail("Aucun tenant authentifié.", "Unauthorized"));
        var columns = request.Columns ?? new List<string>();
        var result = await _schema.QueryAsync(tenantId, request.Table, columns, request.Search, request.Page, request.PageSize, cancellationToken);
        if (result is null)
            return NotFound(ApiResponse<object>.Fail("Table non autorisée ou introuvable.", "Forbidden"));

        var liveColumns = await _schema.ListColumnsAsync(tenantId, request.Table, cancellationToken);
        var viewColumns = columns.Select(c => new ViewColumn { Name = c }).ToList();
        var enriched = await _enricher.EnrichAsync(
            tenantId, request.Table, viewColumns, liveColumns ?? Array.Empty<SqlColumnInfo>(), result, cancellationToken);
        return Ok(ApiResponse<SqlQueryResultDto>.Ok(enriched));
    }

    private bool TryTenant(out Guid tenantId)
    {
        tenantId = _currentUser.TenantId ?? Guid.Empty;
        return tenantId != Guid.Empty;
    }
}
