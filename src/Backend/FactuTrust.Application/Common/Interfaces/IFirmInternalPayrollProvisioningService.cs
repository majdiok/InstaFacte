using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmInternalPayrollProvisioningService
{
    Task<FirmPayrollProvisioningStatusDto> GetProvisioningStatusAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default);

    Task<Result<FirmPayrollProvisionResultDto>> ProvisionFromCollaboratorsAsync(
        Guid firmTenantId,
        bool isManager,
        CancellationToken cancellationToken = default);

    Task<Result<FirmPayrollProvisionResultDto>> ProvisionCollaboratorAsync(
        Guid firmTenantId,
        bool isManager,
        Guid collaboratorUserId,
        FirmCollaboratorPayrollOnboardingDto? onboarding = null,
        CancellationToken cancellationToken = default);
}
