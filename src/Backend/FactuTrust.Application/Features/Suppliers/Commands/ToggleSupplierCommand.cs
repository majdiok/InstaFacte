using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Suppliers.Commands;

/// <summary>
/// Command to toggle supplier active/inactive status.
/// </summary>
public sealed record ToggleSupplierCommand(Guid Id) : IRequest<Result<SupplierDetailDto>>;

/// <summary>
/// Handler for ToggleSupplierCommand.
/// </summary>
public sealed class ToggleSupplierCommandHandler : IRequestHandler<ToggleSupplierCommand, Result<SupplierDetailDto>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IWithholdingTaxRepository _withholdingTaxRepository;

    public ToggleSupplierCommandHandler(
        ISupplierRepository supplierRepository,
        IWithholdingTaxRepository withholdingTaxRepository)
    {
        _supplierRepository = supplierRepository;
        _withholdingTaxRepository = withholdingTaxRepository;
    }

    public async Task<Result<SupplierDetailDto>> Handle(ToggleSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier is null)
            return Result.Failure<SupplierDetailDto>(Error.NotFound("Supplier", request.Id));

        if (supplier.IsActive)
            supplier.Deactivate();
        else
            supplier.Reactivate();

        await _supplierRepository.UpdateAsync(supplier, cancellationToken);

        var dto = await SupplierDetailMapper.ToDetailDtoAsync(supplier, _withholdingTaxRepository, cancellationToken);
        return Result.Success(dto);
    }
}
