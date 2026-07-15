import { Injectable } from '@angular/core';

/**
 * Service centralisé pour l'aperçu avant impression.
 * Ouvre les PDF dans un nouvel onglet afin que l'utilisateur puisse
 * consulter le document et utiliser l'aperçu natif du navigateur (Ctrl+P).
 */
@Injectable({ providedIn: 'root' })
export class PrintPreviewService {
  /**
   * Ouvre un PDF dans un nouvel onglet pour aperçu et impression.
   * L'utilisateur peut ensuite utiliser Fichier > Imprimer ou Ctrl+P
   * pour afficher l'aperçu natif du navigateur.
   * @param blob Contenu PDF
   * @param _filename Optionnel, réservé pour usage futur (ex. titre d'onglet)
   */
  openPdfForPrintPreview(blob: Blob, _filename?: string): void {
    if (!blob || blob.size === 0) return;
    const url = URL.createObjectURL(blob);
    const opened = window.open(url, '_blank', 'noopener,noreferrer');
    if (!opened) {
      URL.revokeObjectURL(url);
      return;
    }
    opened.addEventListener('unload', () => URL.revokeObjectURL(url), { once: true });
  }
}
