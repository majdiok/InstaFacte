using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Resolves the signed fiscal stamp amount from the tenant tax catalog (fixed stamp tax).
/// </summary>
public interface IFiscalStampResolver
{
    /// <param name="isCreditNote">When true, the stamp is negated (e.g. -1 TND).</param>
    Task<Money> ResolveSignedStampAsync(bool isCreditNote, CancellationToken cancellationToken = default);
}
