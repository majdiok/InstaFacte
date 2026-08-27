namespace FactuTrust.Domain.Entities.RecurringContracts;

/// <summary>
/// Ligne cible d'un avenant sur contrat actif (<see cref="RecurringContract.AmendLines"/>).
/// <paramref name="SourceLineId"/> porte l'identifiant de la ligne existante que cette cible
/// remplace quand il est fourni (correspondance prioritaire à la clé LineType + Description) ;
/// la ligne elle-même porte sa propre fenêtre d'effet (EffectiveFrom = date d'effet).
/// </summary>
public sealed record RecurringContractAmendLineTarget(Guid? SourceLineId, RecurringContractLine Line);
