using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Suppliers.Queries;

/// <summary>
/// Query to get supplier details by ID.
/// </summary>
public sealed record GetSupplierByIdQuery(Guid Id) : IRequest<Result<SupplierDetailDto>>;

/// <summary>
/// Handler for GetSupplierByIdQuery.
/// </summary>
public sealed class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, Result<SupplierDetailDto>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IWithholdingTaxRepository _withholdingTaxRepository;

    public GetSupplierByIdQueryHandler(
        ISupplierRepository supplierRepository,
        IWithholdingTaxRepository withholdingTaxRepository)
    {
        _supplierRepository = supplierRepository;
        _withholdingTaxRepository = withholdingTaxRepository;
    }

    public async Task<Result<SupplierDetailDto>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier is null)
            return Result.Failure<SupplierDetailDto>(Error.NotFound("Supplier", request.Id));

        var dto = await SupplierDetailMapper.ToDetailDtoAsync(supplier, _withholdingTaxRepository, cancellationToken);
        return Result.Success(dto);
    }
}
