/**
 * Contrôles du réglage « Modèle Studio avancé » (Studio IA — PR 1.1).
 *
 * Le modèle avancé n'a de sens que s'il est PLUS capable que le modèle Studio standard : le
 * serveur refuse (400) un modèle avancé identique au standard. On le signale ici avant l'envoi
 * pour éviter un aller-retour et un message d'erreur brut.
 */
export function isStudioAdvancedModelSameAsStandard(params: {
  standardModelRef: string | null | undefined;
  advancedModelRef: string | null | undefined;
}): boolean {
  const standard = params.standardModelRef?.trim().toLowerCase();
  const advanced = params.advancedModelRef?.trim().toLowerCase();
  if (!standard || !advanced) {
    return false;
  }

  return standard === advanced;
}
