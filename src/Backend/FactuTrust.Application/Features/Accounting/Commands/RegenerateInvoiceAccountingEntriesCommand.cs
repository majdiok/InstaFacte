using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Accounting.Commands;

/// <summary>
/// Command to regenerate missing journal entries for validated invoices.
/// This is a remediation command for invoices that were validated before
/// the chart of accounts was complete (e.g., missing account 4478).
/// 
/// The command is idempotent: invoices that already have a journal entry
/// (source = "Invoice") are skipped automatically by the accounting service.
/// </summary>
public sealed record RegenerateInvoiceAccountingEntriesCommand : IRequest<Result<RegenerateInvoiceAccountingEntriesResult>>;

/// <summary>
/// Result of the regeneration command.
/// </summary>
public sealed class RegenerateInvoiceAccountingEntriesResult
{
    public int TotalInvoicesScanned { get; set; }
    public int EntriesCreated { get; set; }
    public int AlreadyExisted { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Handler for RegenerateInvoiceAccountingEntriesCommand.
/// Scans all validated/signed/paid invoices and regenerates their accounting entries
/// if they are missing.
/// </summary>
public sealed class RegenerateInvoiceAccountingEntriesCommandHandler
    : IRequestHandler<RegenerateInvoiceAccountingEntriesCommand, Result<RegenerateInvoiceAccountingEntriesResult>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IAccountingService _accountingService;
    private readonly IAuditService _auditService;
    private readonly ILogger<RegenerateInvoiceAccountingEntriesCommandHandler> _logger;

    public RegenerateInvoiceAccountingEntriesCommandHandler(
        IInvoiceRepository invoiceRepository,
        IAccountingService accountingService,
        IAuditService auditService,
        ILogger<RegenerateInvoiceAccountingEntriesCommandHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _accountingService = accountingService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<RegenerateInvoiceAccountingEntriesResult>> Handle(
        RegenerateInvoiceAccountingEntriesCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting regeneration of missing invoice accounting entries");

        // Get all invoices that should have accounting entries
        // (Validated, Signed, Paid, PartiallyPaid, Overdue, Archived)
        var eligibleStatuses = new[]
        {
            InvoiceStatus.Validated,
            InvoiceStatus.Signed,
            InvoiceStatus.Paid,
            InvoiceStatus.PartiallyPaid,
            InvoiceStatus.Overdue,
            InvoiceStatus.Archived
        };

        var allInvoices = await _invoiceRepository.GetAllAsync(cancellationToken);
        var eligibleInvoices = allInvoices
            .Where(i => eligibleStatuses.Contains(i.Status))
            .OrderBy(i => i.IssueDate)
            .ToList();

        var result = new RegenerateInvoiceAccountingEntriesResult
        {
            TotalInvoicesScanned = eligibleInvoices.Count
        };

        _logger.LogInformation("Found {Count} eligible invoices for accounting entry regeneration",
            eligibleInvoices.Count);

        foreach (var invoice in eligibleInvoices)
        {
            try
            {
                // Load invoice with lines (needed for RevenueAccountForLine)
                var invoiceWithLines = await _invoiceRepository.GetByIdWithLinesAsync(
                    invoice.Id, cancellationToken);

                if (invoiceWithLines is null)
                {
                    _logger.LogWarning("Invoice {InvoiceId} not found when loading with lines", invoice.Id);
                    result.Failed++;
                    result.Errors.Add($"Invoice {invoice.Id} not found");
                    continue;
                }

                // Both methods are idempotent (check GetBySourceAsync first).
                // Route to the credit-note variant when the invoice is an AVO.
                var entryResult = invoiceWithLines.Type == InvoiceType.CreditNote
                    ? await _accountingService.GenerateInvoiceCreditNoteEntryAsync(
                        invoiceWithLines, cancellationToken)
                    : await _accountingService.GenerateInvoiceSaleEntryAsync(
                        invoiceWithLines, cancellationToken);

                if (entryResult.IsSuccess)
                {
                    // The service returns Success both when a new entry is created
                    // and when one already exists (idempotent skip).
                    // We log the attempt either way.
                    result.EntriesCreated++;
                    _logger.LogInformation(
                        "Successfully processed accounting entry for invoice {InvoiceNumber} ({InvoiceId})",
                        invoiceWithLines.Number.Value, invoiceWithLines.Id);
                }
                else
                {
                    result.Failed++;
                    result.Errors.Add($"{invoiceWithLines.Number.Value}: {entryResult.Error.Description}");
                    _logger.LogWarning(
                        "Failed to regenerate accounting entry for invoice {InvoiceNumber}: {Error}",
                        invoiceWithLines.Number.Value, entryResult.Error.Description);
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Invoice {invoice.Id}: {ex.Message}");
                _logger.LogError(ex,
                    "Exception while regenerating accounting entry for invoice {InvoiceId}", invoice.Id);
            }
        }

        _logger.LogInformation(
            "Accounting entry regeneration complete: {Created} created, {Skipped} already existed, {Failed} failed out of {Total} invoices",
            result.EntriesCreated, result.AlreadyExisted, result.Failed, result.TotalInvoicesScanned);

        await _auditService.LogAsync(
            "accounting.entries.regenerated",
            "System",
            Guid.Empty,
            newValues: new
            {
                result.TotalInvoicesScanned,
                result.EntriesCreated,
                result.AlreadyExisted,
                result.Failed
            },
            cancellationToken: cancellationToken);

        return Result.Success(result);
    }
}
