import pathlib
ROOT = pathlib.Path(r"c:\Solution\FactuTrust - Copy\src\Backend")

DOCUMENT_NUMBER_SERVICE = """using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

public sealed class DocumentNumberService : IDocumentNumberService
{
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;
    private readonly ILogger<DocumentNumberService> _logger;

    public DocumentNumberService(
        IDbContextFactory<TenantDbContext> contextFactory,
        ILogger<DocumentNumberService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<DocumentNumberResult> ReserveNextAsync(
        Guid tenantId,
        NumberingDocumentType documentType,
        int fiscalYear,
        DateTime referenceDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                var scheme = await GetOrCreateSchemeTrackedAsync(
                    context, tenantId, documentType, fiscalYear, cancellationToken);

                var sequence = scheme.ReserveNextSequence();
                var blocks = scheme.GetBlocks();
                var freeText = blocks.FirstOrDefault(b => b.Type == NumberingBlockType.FreeText)?.Value
                    ?? documentType.DefaultFreeText();

                var renderResult = NumberingFormatRenderer.Render(
                    blocks,
                    new NumberingFormatRenderer.RenderContext(sequence, referenceDate, freeText));

                if (renderResult.IsFailure)
                    throw new InvalidOperationException(renderResult.Error.Description);

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Reserved {DocumentType} number {Number} for tenant {TenantId}",
                    documentType, renderResult.Value, tenantId);

                return new DocumentNumberResult(
                    renderResult.Value,
                    referenceDate.Year,
                    sequence,
                    freeText);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<string> PreviewNextAsync(
        Guid tenantId,
        NumberingDocumentType documentType,
        int fiscalYear,
        DateTime referenceDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var scheme = await context.DocumentNumberingSchemes
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.DocumentType == documentType && s.FiscalYear == fiscalYear)
            .FirstOrDefaultAsync(cancellationToken);

        if (scheme is null)
        {
            var legacySequence = await GetLegacyCurrentSequenceAsync(
                context, tenantId, documentType, fiscalYear, cancellationToken);
            var blocks = NumberingSchemeDefaults.GetDefaultBlocks(documentType);
            var freeText = blocks.FirstOrDefault(b => b.Type == NumberingBlockType.FreeText)?.Value
                ?? documentType.DefaultFreeText();
            var nextSeq = Math.Max(legacySequence + 1, 1);
            var render = NumberingFormatRenderer.Render(
                blocks,
                new NumberingFormatRenderer.RenderContext(nextSeq, referenceDate, freeText));
            return render.IsSuccess ? render.Value : string.Empty;
        }

        var nextSequence = scheme.PreviewNextSequence();
        var schemeBlocks = scheme.GetBlocks();
        var prefix = schemeBlocks.FirstOrDefault(b => b.Type == NumberingBlockType.FreeText)?.Value
            ?? documentType.DefaultFreeText();
        var result = NumberingFormatRenderer.Render(
            schemeBlocks,
            new NumberingFormatRenderer.RenderContext(nextSequence, referenceDate, prefix));
        return result.IsSuccess ? result.Value : string.Empty;
    }

    private static async Task<DocumentNumberingScheme> GetOrCreateSchemeTrackedAsync(
        TenantDbContext context,
        Guid tenantId,
        NumberingDocumentType documentType,
        int fiscalYear,
        CancellationToken cancellationToken)
    {
        var scheme = await context.DocumentNumberingSchemes
            .Where(s => s.TenantId == tenantId && s.DocumentType == documentType && s.FiscalYear == fiscalYear)
            .FirstOrDefaultAsync(cancellationToken);

        if (scheme is not null)
            return scheme;

        var legacySequence = await GetLegacyCurrentSequenceAsync(
            context, tenantId, documentType, fiscalYear, cancellationToken);

        scheme = DocumentNumberingScheme.CreateDefault(tenantId, documentType, fiscalYear, legacySequence);
        context.DocumentNumberingSchemes.Add(scheme);
        return scheme;
    }

    private static async Task<int> GetLegacyCurrentSequenceAsync(
        TenantDbContext context,
        Guid tenantId,
        NumberingDocumentType documentType,
        int fiscalYear,
        CancellationToken cancellationToken)
    {
        var prefix = documentType.LegacyPrefix();

        return documentType switch
        {
            NumberingDocumentType.Invoice or NumberingDocumentType.CreditNote =>
                await context.InvoiceNumberSequences.AsNoTracking()
                    .Where(s => s.TenantId == tenantId && s.Prefix == prefix && s.FiscalYear == fiscalYear)
                    .Select(s => (int?)s.CurrentSequence)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0,

            NumberingDocumentType.Quote =>
                await context.QuoteNumberSequences.AsNoTracking()
                    .Where(s => s.TenantId == tenantId && s.Prefix == prefix && s.FiscalYear == fiscalYear)
                    .Select(s => (int?)s.CurrentSequence)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0,

            NumberingDocumentType.CashReceipt or NumberingDocumentType.CashExpense =>
                await context.CashOperationNumberSequences.AsNoTracking()
                    .Where(s => s.TenantId == tenantId && s.Prefix == prefix && s.FiscalYear == fiscalYear)
                    .Select(s => (int?)s.CurrentSequence)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0,

            NumberingDocumentType.BankDeposit =>
                await context.BankDepositNumberSequences.AsNoTracking()
                    .Where(s => s.TenantId == tenantId && s.FiscalYear == fiscalYear)
                    .Select(s => (int?)s.CurrentSequence)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0,

            NumberingDocumentType.PhysicalInventory =>
                await context.InventoryNumberSequences.AsNoTracking()
                    .Where(s => s.Year == fiscalYear)
                    .Select(s => (int?)s.LastSequence)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0,

            _ => 0
        };
    }
}
"""

NUMBERING_CONTROLLER = """using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Numbering.Commands;
using FactuTrust.Application.Features.Numbering.Queries;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/settings/numbering")]
[Authorize]
public sealed class NumberingController : ControllerBase
{
    private readonly IMediator _mediator;

    public NumberingController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NumberingSchemeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int? fiscalYear, CancellationToken cancellationToken)
    {
        var schemes = await _mediator.Send(new GetNumberingSchemesQuery(fiscalYear), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NumberingSchemeDto>>.Ok(schemes));
    }

    [HttpGet("{documentType}")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<NumberingSchemeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByType(
        NumberingDocumentType documentType,
        [FromQuery] int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var scheme = await _mediator.Send(new GetNumberingSchemeByTypeQuery(documentType, fiscalYear), cancellationToken);
        return Ok(ApiResponse<NumberingSchemeDto>.Ok(scheme));
    }

    [HttpPut("{documentType}")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<NumberingSchemeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save(
        NumberingDocumentType documentType,
        [FromBody] SaveNumberingSchemeRequest request,
        [FromQuery] int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SaveNumberingSchemeCommand(documentType, request, fiscalYear), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<NumberingSchemeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<NumberingSchemeDto>.Ok(result.Value));
    }

    [HttpPost("{documentType}/preview")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<PreviewNumberingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        NumberingDocumentType documentType,
        [FromBody] PreviewNumberingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _mediator.Send(new PreviewNumberingQuery(documentType, request), cancellationToken);
            return Ok(ApiResponse<PreviewNumberingResponse>.Ok(response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PreviewNumberingResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("{documentType}/reset")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<NumberingSchemeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reset(
        NumberingDocumentType documentType,
        [FromQuery] int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ResetNumberingSchemeCommand(documentType, fiscalYear), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<NumberingSchemeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<NumberingSchemeDto>.Ok(result.Value));
    }
}
"""

(ROOT / "FactuTrust.Infrastructure/Services/DocumentNumberService.cs").write_text(DOCUMENT_NUMBER_SERVICE, encoding="utf-8")
(ROOT / "FactuTrust.API/Controllers/NumberingController.cs").write_text(NUMBERING_CONTROLLER, encoding="utf-8")
print("ok")
