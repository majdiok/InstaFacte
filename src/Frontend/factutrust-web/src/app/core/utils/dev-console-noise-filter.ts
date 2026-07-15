/**
 * Filtre de bruit console — À N'UTILISER QU'EN DÉVELOPPEMENT.
 *
 * Objectif : neutraliser le rejet de promesse non géré injecté par des EXTENSIONS de navigateur
 * (et non par l'application), qui pollue la console DevTools sous la forme :
 *   `Uncaught (in promise) Error: Could not establish connection. Receiving end does not exist.`
 * Cette erreur ne provient PAS du code de FactuTrust (aucune occurrence dans `src/`).
 *
 * Garde-fous anti-régression :
 *   - Activé UNIQUEMENT en dev (appelant gardé par `isDevMode()` dans main.ts) → prod intacte.
 *   - Correspondance sur chaîne EXACTE → ne masque jamais un vrai rejet applicatif.
 *   - N'enveloppe PAS `console.error`/`console.warn` : l'attribution des sources d'erreurs dans
 *     DevTools reste correcte (un wrapper console ferait pointer toutes les erreurs vers ce fichier).
 *
 * Bruit non interceptable depuis la page (à masquer via le toggle DevTools « Hide messages from
 * extensions » — voir docs/developer/console-errors-faq.md) :
 *   - `[MindStudio][Messaging] …` (logué par le content script de l'extension via console.error).
 *   - `Unchecked runtime.lastError: …` (émis par Chrome lui-même).
 *   - Rejets émis dans le « monde isolé » d'un content script (n'atteignent pas `window`).
 */

/** Signature exacte des erreurs de messagerie d'extensions Chrome (content script ↔ service worker). */
const EXTENSION_MESSAGING_SIGNATURE = 'Could not establish connection. Receiving end does not exist.';

let installed = false;

/**
 * Installe le filtre de bruit console. Idempotent et sans effet hors navigateur.
 * Ne JAMAIS appeler en production (réservé au débogage local).
 */
export function installDevConsoleNoiseFilter(): void {
  if (installed || typeof window === 'undefined') {
    return;
  }
  installed = true;

  // Rejets de promesses non gérés émis par des extensions (lignes « Uncaught (in promise) »).
  window.addEventListener('unhandledrejection', (event: PromiseRejectionEvent) => {
    if (isExtensionMessagingNoise(event.reason)) {
      // Empêche l'affichage console de CETTE signature précise uniquement. Tout autre rejet
      // (y compris un vrai bug applicatif) reste affiché normalement.
      event.preventDefault();
    }
  });
}

function isExtensionMessagingNoise(reason: unknown): boolean {
  if (typeof reason === 'string') {
    return reason === EXTENSION_MESSAGING_SIGNATURE;
  }
  if (reason && typeof reason === 'object' && 'message' in reason) {
    return (reason as { message?: unknown }).message === EXTENSION_MESSAGING_SIGNATURE;
  }
  return false;
}
