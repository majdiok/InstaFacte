using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IVatDeclarationRepository
{
    Task<VatDeclaration?> GetByYearMonthAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<VatDeclaration> AddAsync(VatDeclaration entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(VatDeclaration entity, CancellationToken cancellationToken = default);
}
