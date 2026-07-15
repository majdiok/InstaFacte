using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>Builds TEJ RS XML payloads from paid supplier invoices (no persisted certificates).</summary>
public interface ISupplierInvoiceTejDeclarationBuilder
{
    Task<Result<IReadOnlyList<TejRsCertificatPayload>>> BuildForPeriodAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
