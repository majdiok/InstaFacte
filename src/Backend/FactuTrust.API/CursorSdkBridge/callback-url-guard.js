// CWE-918 (SSRF) hardening: callToolCallback in bridge.js is only ever supposed to reach back
// into the local FactuTrust API (see BuildCursorToolCallbackUrl in SendChatMessageCommand.cs,
// which defaults to http://127.0.0.1:7000 and is otherwise driven by
// CursorSdkSettings:ToolCallbackBaseUrl). Extracted into its own module (like
// bridge-agent-options.js) so it can be unit-tested without starting the bridge's HTTP server.
const LOOPBACK_HOSTNAMES = new Set(["127.0.0.1", "localhost", "::1", "[::1]"]);

/**
 * Returns true only for http(s) URLs whose hostname is a loopback address.
 * Anything else (non-loopback host, non-http(s) scheme, unparsable URL) is rejected.
 * @param {string} rawUrl
 * @returns {boolean}
 */
export function isAllowedCallbackUrl(rawUrl) {
  let parsed;
  try {
    parsed = new URL(rawUrl);
  } catch {
    return false;
  }
  if (parsed.protocol !== "http:" && parsed.protocol !== "https:") return false;
  return LOOPBACK_HOSTNAMES.has(parsed.hostname.toLowerCase());
}
