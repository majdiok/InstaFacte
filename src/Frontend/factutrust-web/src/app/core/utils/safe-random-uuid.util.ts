/**
 * UUID v4 client-side, safe outside secure contexts (HTTP + LAN IP).
 *
 * Chromium exposes `crypto.randomUUID` only on HTTPS / localhost.
 * Lab access via `http://192.168.x.x` therefore throws TypeError without this helper.
 */
const UUID_V4_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function createClientUuid(): string {
  const c = globalThis.crypto as Crypto | undefined;

  if (c && typeof c.randomUUID === 'function') {
    return c.randomUUID();
  }

  if (c && typeof c.getRandomValues === 'function') {
    return uuidV4FromBytes(c.getRandomValues(new Uint8Array(16)));
  }

  return uuidV4FromMathRandom();
}

/** Exposed for unit tests that assert format without depending on browser APIs. */
export function isUuidV4(value: string): boolean {
  return UUID_V4_RE.test(value);
}

function uuidV4FromBytes(bytes: Uint8Array): string {
  // RFC 4122 §4.4 — set version (4) and variant (10xxxxxx).
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  return formatUuid(bytes);
}

function uuidV4FromMathRandom(): string {
  const bytes = new Uint8Array(16);
  for (let i = 0; i < 16; i++) {
    bytes[i] = Math.floor(Math.random() * 256);
  }
  return uuidV4FromBytes(bytes);
}

function formatUuid(bytes: Uint8Array): string {
  let hex = '';
  for (let i = 0; i < bytes.length; i++) {
    hex += bytes[i].toString(16).padStart(2, '0');
  }
  return (
    hex.slice(0, 8) +
    '-' +
    hex.slice(8, 12) +
    '-' +
    hex.slice(12, 16) +
    '-' +
    hex.slice(16, 20) +
    '-' +
    hex.slice(20, 32)
  );
}
