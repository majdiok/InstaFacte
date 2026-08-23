using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.RecurringContracts;

public sealed class RecurringContractInvoiceLinker : IRecurringContractInvoiceLinker
{
    private readonly ITenantDbContextFactory _factory;

    public RecurringContractInvoiceLinker(ITenantDbContextFactory factory)
    {
        _factory = factory;
    }

    public async Task LinkIfRecurringDraftAsync(
        Guid draftId, Invoice invoice, CancellationToken cancellationToken)
    {
        await using var db = _factory.CreateContext();
        var run = await db.RecurringContractBillingRuns
            .FirstOrDefaultAsync(r =>
                r.InvoiceDraftId == draftId &&
                r.Status == Domain.Enums.RecurringContractBillingRunStatus.DraftCreated,
                cancellationToken);
        if (run is null) return;

        invoice.SetRecurringContractSource(run.RecurringContractId, run.Id);
    }
}
