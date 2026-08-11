using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Reporte les congés du cabinet vers sa paie interne.
/// </summary>
/// <remarks>
/// <para>
/// Les congés du cabinet sont la source unique de vérité. Ce service en tire le congé de paie
/// correspondant dans le tenant du cabinet, ce qui déclenche naturellement les mécanismes déjà
/// en place : retenue pour absence non rémunérée, indemnités journalières CNSS, acquisition de
/// droits à la validation du cycle. Aucun calcul n'est refait ici — seuls les jours arrêtés
/// côté cabinet sont transmis.
/// </para>
/// <para>
/// Le report ne bloque jamais l'acte RH : une base de paie éteinte ne doit pas empêcher un
/// responsable de valider un congé. L'issue est consignée sur la demande et rattrapable depuis
/// l'écran de rapprochement.
/// </para>
/// </remarks>
public interface IFirmLeavePayrollMirrorService
{
    /// <summary>Reporte une demande approuvée, ou met à jour son report existant.</summary>
    Task<FirmLeaveMirrorResultDto> MirrorApprovedAsync(
        Guid firmTenantId,
        Guid firmLeaveRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>Retire le congé de paie produit par une demande qui ne le justifie plus.</summary>
    Task<FirmLeaveMirrorResultDto> RevokeAsync(
        Guid firmTenantId,
        Guid firmLeaveRequestId,
        CancellationToken cancellationToken = default);
}
