/**
 * Filtre de bruit console — À N'UTILISER QU'EN DÉVELOPPEMENT.
 *
 * Objectif : neutraliser le bruit injecté par des EXTENSIONS de navigateur (et non par
 * l'application), notamment :
 *   - `Uncaught (in promise) Error: Could not establish connection. Receiving end does not exist.`
 *   - `[Auth] Failed to get auth status:` + `message port closed` / `Receiving end does not exist`
 *   - `TypeError: Cannot read properties of undefined (reading 'toLowerCase')` depuis `keyboard.ts-*.js`
 *     (gestionnaire de mots de passe / assistant clavier — aucun `keyboard.ts` dans ce dépôt)
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
    if (isExtensionMessagingNoise(event.reason) || isExtensionKeyboardNoise(event.reason)) {
      event.preventDefault();
    }
  });

  // Erreurs synchrones / Uncaught TypeError depuis scripts d'extension (ex. keyboard.ts-*.js).
  window.addEventListener('error', (event: ErrorEvent) => {
    if (
      isExtensionKeyboardNoise(event.error) ||
      isExtensionKeyboardNoise(event.message) ||
      isExtensionKeyboardNoiseFromFilename(event.filename, event.message)
    ) {
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

/**
 * Gestionnaires de mots de passe / assistants clavier injectent souvent un chunk
 * `keyboard.ts-*.js` qui appelle `.toLowerCase()` sur une valeur undefined.
 */
export function isExtensionKeyboardNoise(reason: unknown): boolean {
  const text = extractNoiseText(reason);
  return matchesKeyboardExtensionNoise(text);
}

export function isExtensionConsoleNoise(args: unknown[]): boolean {
  if (args.some((arg) => matchesKnownExtensionMessage(extractMessage(arg)))) {
    return true;
  }
  return matchesKeyboardExtensionNoise(args.map((arg) => extractNoiseText(arg) ?? '').join('\n'));
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

/** Message + stack (si présents) pour matcher les piles d'extensions. */
function extractNoiseText(value: unknown): string | null {
  if (typeof value === 'string') {
    return value;
  }
  if (value && typeof value === 'object') {
    const parts: string[] = [];
    const message = extractMessage(value);
    if (message) {
      parts.push(message);
    }
    if ('stack' in value && typeof (value as { stack?: unknown }).stack === 'string') {
      parts.push((value as { stack: string }).stack);
    }
    if (parts.length > 0) {
      return parts.join('\n');
    }
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

function matchesKeyboardExtensionNoise(text: string | null): boolean {
  if (!text) {
    return false;
  }
  const lower = text.toLowerCase();
  return lower.includes('keyboard.ts') && lower.includes('tolowercase');
}

function isExtensionKeyboardNoiseFromFilename(filename: string | undefined, message: string): boolean {
  if (!filename || !message) {
    return false;
  }
  return /keyboard\.ts/i.test(filename) && /toLowerCase/i.test(message);
}
