/**
 * Filtre de bruit console — À N'UTILISER QU'EN DÉVELOPPEMENT.
 *
 * Objectif : neutraliser le bruit injecté par des EXTENSIONS de navigateur (et non par
 * l'application), notamment :
 *   - `Uncaught (in promise) Error: Could not establish connection. Receiving end does not exist.`
 *   - `[Auth] Failed to get auth status:` + `message port closed` / `Receiving end does not exist`
 * Ces messages ne proviennent PAS du code FactuTrust (aucune occurrence dans `src/`).
 *
 * Garde-fous anti-régression :
 *   - Activé UNIQUEMENT en dev (appelant gardé par `isDevMode()` dans main.ts) → prod intacte.
 *   - Correspondance sur signatures documentées → ne masque jamais un vrai log applicatif.
 *
 * Bruit non interceptable depuis la page (à masquer via le toggle DevTools « Hide messages from
 * extensions » — voir docs/developer/console-errors-faq.md) :
 *   - `[MindStudio][Messaging] …` (logué par le content script de l'extension via console.error).
 *   - `Unchecked runtime.lastError: …` (émis par Chrome lui-même).
 *   - Logs émis dans le « monde isolé » d'un content script (n'atteignent pas `window`).
 */

/** Signature exacte des erreurs de messagerie d'extensions Chrome (content script ↔ service worker). */
const EXTENSION_MESSAGING_SIGNATURE = 'Could not establish connection. Receiving end does not exist.';
const EXTENSION_MESSAGE_PORT_CLOSED = 'The message port closed before a response was received.';
const EXTENSION_AUTH_PREFIX = '[Auth] Failed to get auth status';

let installed = false;
let originalConsoleWarn: typeof console.warn | null = null;
let originalConsoleError: typeof console.error | null = null;

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
      event.preventDefault();
    }
  });

  originalConsoleWarn = console.warn.bind(console);
  originalConsoleError = console.error.bind(console);

  console.warn = (...args: unknown[]) => {
    if (!isExtensionConsoleNoise(args)) {
      originalConsoleWarn!(...args);
    }
  };

  console.error = (...args: unknown[]) => {
    if (!isExtensionConsoleNoise(args)) {
      originalConsoleError!(...args);
    }
  };
}

/** @internal Réinitialise l'état du filtre (tests uniquement). */
export function resetDevConsoleNoiseFilterForTests(): void {
  if (originalConsoleWarn) {
    console.warn = originalConsoleWarn;
  }
  if (originalConsoleError) {
    console.error = originalConsoleError;
  }
  installed = false;
  originalConsoleWarn = null;
  originalConsoleError = null;
}

export function isExtensionMessagingNoise(reason: unknown): boolean {
  return matchesKnownExtensionMessage(extractMessage(reason));
}

export function isExtensionConsoleNoise(args: unknown[]): boolean {
  return args.some((arg) => matchesKnownExtensionMessage(extractMessage(arg)));
}

function extractMessage(value: unknown): string | null {
  if (typeof value === 'string') {
    return value;
  }
  if (value && typeof value === 'object' && 'message' in value) {
    const message = (value as { message?: unknown }).message;
    return typeof message === 'string' ? message : null;
  }
  return null;
}

function matchesKnownExtensionMessage(message: string | null): boolean {
  if (!message) {
    return false;
  }
  if (message === EXTENSION_MESSAGING_SIGNATURE) {
    return true;
  }
  if (message === EXTENSION_MESSAGE_PORT_CLOSED) {
    return true;
  }
  if (message.startsWith(EXTENSION_AUTH_PREFIX)) {
    return true;
  }
  return false;
}
