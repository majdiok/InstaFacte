using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IWithholdingTaxRepository
{
    Task<List<WithholdingTaxType>> GetAllTypesAsync(CancellationToken ct = default);
    Task<List<WithholdingTaxType>> GetActiveTypesAsync(CancellationToken ct = default);
    Task<WithholdingTaxType?> GetTypeByIdAsync(Guid id, CancellationToken ct = default);
    Task<WithholdingTaxType?> GetTypeByCodeAsync(string code, CancellationToken ct = default);
    Task AddTypeAsync(WithholdingTaxType type, CancellationToken ct = default);
    Task UpdateTypeAsync(WithholdingTaxType type, CancellationToken ct = default);
}
