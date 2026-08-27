using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Events;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.RecurringContracts;

/// <summary>
/// Links validated invoices back to recurring contract billing runs.
/// </summary>
public sealed class LinkRecurringContractBillingRunOnInvoiceValidatedHandler
    : INotificationHandler<InvoiceValidatedEvent>
{
    private readonly ITenantDbContextFactory _tenantFactory;

    public LinkRecurringContractBillingRunOnInvoiceValidatedHandler(ITenantDbContextFactory tenantFactory)
    {
        _tenantFactory = tenantFactory;
    }

    public async Task Handle(InvoiceValidatedEvent notification, CancellationToken cancellationToken)
    {
        // CreateContext (et non CreateIsolatedContext) : l'événement est dispatché depuis
        // SaveChangesAsync alors que la transaction du UnitOfWork est encore ouverte. Un
        // contexte isolé utiliserait une AUTRE connexion, bloquée par les verrous de cette
        // transaction (timeout SQL), et la liaison serait perdue silencieusement. Le
        // contexte ambiant s'enrôle dans la transaction en cours : même connexion, pas de
        // blocage, et le marquage Invoiced est commité atomiquement avec la validation.
        await using var db = _tenantFactory.CreateContext();
        var invoice = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == notification.InvoiceId, cancellationToken);
        if (invoice?.SourceRecurringContractBillingRunId is null)
            return;

        var run = await db.RecurringContractBillingRuns
            .FirstOrDefaultAsync(r => r.Id == invoice.SourceRecurringContractBillingRunId.Value, cancellationToken);
        if (run is null) return;

        run.MarkInvoiced(invoice.Id);
        await db.SaveChangesAsync(cancellationToken);
    }
}
