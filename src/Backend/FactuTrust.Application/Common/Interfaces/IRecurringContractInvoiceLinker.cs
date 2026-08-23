using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces;

public interface IRecurringContractInvoiceLinker
{
    Task LinkIfRecurringDraftAsync(Guid draftId, Invoice invoice, CancellationToken cancellationToken);
}
