import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { isAllowedCallbackUrl } from "./callback-url-guard.js";

describe("callback-url-guard", () => {
  it("accepts http(s) loopback URLs", () => {
    assert.ok(isAllowedCallbackUrl("http://127.0.0.1:7000/internal/ai/cursor-tools/abc"));
    assert.ok(isAllowedCallbackUrl("http://localhost:7000/internal/ai/cursor-tools/abc"));
    assert.ok(isAllowedCallbackUrl("https://localhost/internal/ai/cursor-tools/abc"));
    assert.ok(isAllowedCallbackUrl("http://[::1]:7000/x"));
  });

  it("rejects non-loopback hosts (SSRF)", () => {
    assert.equal(isAllowedCallbackUrl("http://169.254.169.254/latest/meta-data/"), false);
    assert.equal(isAllowedCallbackUrl("http://example.com/callback"), false);
    assert.equal(isAllowedCallbackUrl("http://10.0.0.5:7000/callback"), false);
    assert.equal(isAllowedCallbackUrl("http://evil.local/callback"), false);
  });

  it("rejects non-http(s) schemes", () => {
    assert.equal(isAllowedCallbackUrl("file:///etc/passwd"), false);
    assert.equal(isAllowedCallbackUrl("ftp://127.0.0.1/x"), false);
  });

  it("rejects unparsable URLs", () => {
    assert.equal(isAllowedCallbackUrl("not a url"), false);
    assert.equal(isAllowedCallbackUrl(""), false);
    assert.equal(isAllowedCallbackUrl(undefined), false);
  });

  it("rejects hostname-confusable strings that are not actually loopback", () => {
    // Guards against naive substring/startsWith checks: these must NOT be treated as loopback.
    assert.equal(isAllowedCallbackUrl("http://127.0.0.1.evil.com/callback"), false);
    assert.equal(isAllowedCallbackUrl("http://localhost.evil.com/callback"), false);
  });
});
