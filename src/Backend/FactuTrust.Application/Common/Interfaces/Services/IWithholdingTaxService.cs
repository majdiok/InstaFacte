using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IWithholdingTaxService
{
    WithholdingCalculationResultDto CalculateWithholding(WithholdingCalculationRequest request);
}
