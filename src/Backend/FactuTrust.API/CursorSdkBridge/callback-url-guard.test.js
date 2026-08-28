import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { isAllowedCallbackUrl } from "./callback-url-guard.js";

describe("callback-url-guard", () => {
  it("accepts http(s) loopback URLs on the expected callback route", () => {
    assert.ok(isAllowedCallbackUrl("http://127.0.0.1:7000/internal/ai/cursor-tools/abc"));
    assert.ok(isAllowedCallbackUrl("http://localhost:7000/internal/ai/cursor-tools/abc"));
    assert.ok(isAllowedCallbackUrl("https://localhost/internal/ai/cursor-tools/abc"));
    assert.ok(isAllowedCallbackUrl("http://[::1]:7000/internal/ai/cursor-tools/9b1e"));
  });

  it("rejects non-loopback hosts (SSRF)", () => {
    assert.equal(isAllowedCallbackUrl("http://169.254.169.254/internal/ai/cursor-tools/abc"), false);
    assert.equal(isAllowedCallbackUrl("http://example.com/internal/ai/cursor-tools/abc"), false);
    assert.equal(isAllowedCallbackUrl("http://10.0.0.5:7000/internal/ai/cursor-tools/abc"), false);
    assert.equal(isAllowedCallbackUrl("http://evil.local/internal/ai/cursor-tools/abc"), false);
  });

  it("rejects non-http(s) schemes", () => {
    assert.equal(isAllowedCallbackUrl("file:///etc/passwd"), false);
    assert.equal(isAllowedCallbackUrl("ftp://127.0.0.1/internal/ai/cursor-tools/abc"), false);
  });

  it("rejects unparsable URLs", () => {
    assert.equal(isAllowedCallbackUrl("not a url"), false);
    assert.equal(isAllowedCallbackUrl(""), false);
    assert.equal(isAllowedCallbackUrl(undefined), false);
  });

  it("rejects hostname-confusable strings that are not actually loopback", () => {
    // Guards against naive substring/startsWith checks: these must NOT be treated as loopback.
    assert.equal(isAllowedCallbackUrl("http://127.0.0.1.evil.com/internal/ai/cursor-tools/abc"), false);
    assert.equal(isAllowedCallbackUrl("http://localhost.evil.com/internal/ai/cursor-tools/abc"), false);
  });

  it("rejects URLs carrying userinfo/credentials even on an otherwise-allowed loopback URL", () => {
    assert.equal(isAllowedCallbackUrl("http://user:pass@127.0.0.1:7000/internal/ai/cursor-tools/abc"), false);
    assert.equal(isAllowedCallbackUrl("http://attacker@localhost:7000/internal/ai/cursor-tools/abc"), false);
  });

  it("rejects loopback URLs whose path is not the expected cursor-tools callback route", () => {
    // Even though the port is configuration-driven and can't be pinned exactly, the route path
    // built by BuildCursorToolCallbackUrl never varies, so anything else must be rejected.
    assert.equal(isAllowedCallbackUrl("http://127.0.0.1:7000/"), false);
    assert.equal(isAllowedCallbackUrl("http://127.0.0.1:7000/admin"), false);
    assert.equal(isAllowedCallbackUrl("http://127.0.0.1:7000/internal/ai/cursor-tools/"), false);
    assert.equal(isAllowedCallbackUrl("http://127.0.0.1:7000/internal/ai/cursor-tools/abc/extra"), false);
    assert.equal(isAllowedCallbackUrl("http://127.0.0.1:6379/"), false);
  });
});
