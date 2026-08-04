using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Création par le cabinet comptable de dossiers clients gérés : société (tenant) sans compte
/// utilisateur sur la plateforme, avec affectation cabinet active et dossier permanent pré-remplis.
/// </summary>
public interface IFirmManagedClientService
{
    Task<Result<FirmManagedClientCreatedDto>> CreateManagedClientAsync(
        Guid firmTenantId,
        Guid firmUserId,
        CreateFirmManagedClientDto dto,
        CancellationToken cancellationToken = default);
}
