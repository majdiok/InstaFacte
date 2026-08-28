// CWE-918 (SSRF) hardening: callToolCallback in bridge.js is only ever supposed to reach back
// into the local FactuTrust API (see BuildCursorToolCallbackUrl in SendChatMessageCommand.cs,
// which defaults to http://127.0.0.1:7000 and is otherwise driven by
// CursorSdkSettings:ToolCallbackBaseUrl). Extracted into its own module (like
// bridge-agent-options.js) so it can be unit-tested without starting the bridge's HTTP server.
const LOOPBACK_HOSTNAMES = new Set(["127.0.0.1", "localhost", "::1", "[::1]"]);

// BuildCursorToolCallbackUrl (SendChatMessageCommand.cs) always constructs the callback path as
// `/internal/ai/cursor-tools/{runId}` where runId is a Guid. The base URL/port is configurable
// (CursorSdkSettings:ToolCallbackBaseUrl), so it can't be pinned to a single fixed port, but the
// route path itself never varies — restricting to it is defense-in-depth against SSRF to *other*
// services that happen to also bind a loopback port (e.g. another local daemon), even though the
// hostname allow-list already rules out non-loopback targets like the cloud metadata endpoint.
const CALLBACK_PATH_PATTERN = /^\/internal\/ai\/cursor-tools\/[^/]+$/;

/**
 * Returns true only for http(s) URLs whose hostname is a loopback address, that carry no
 * userinfo/credentials, and whose path matches the fixed cursor-tools callback route.
 * Anything else (non-loopback host, non-http(s) scheme, credentials in the URL, unexpected path,
 * unparsable URL) is rejected.
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
  // Reject `http://user:pass@host/...` style URLs: userinfo has no legitimate use for this
  // internal loopback callback and could be abused to smuggle credentials or confuse parsers.
  if (parsed.username !== "" || parsed.password !== "") return false;
  if (!LOOPBACK_HOSTNAMES.has(parsed.hostname.toLowerCase())) return false;
  return CALLBACK_PATH_PATTERN.test(parsed.pathname);
}
