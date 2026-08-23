import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  CURSOR_RUN_DISALLOWED_TOOLS,
  CURSOR_RUN_TOOLS,
  assertValidAgentToolOptions,
  buildLocalAgentOptions
} from "./bridge-agent-options.js";

describe("bridge-agent-options", () => {
    it("does not include invalid public tool name write", () => {
      assert.ok(!CURSOR_RUN_DISALLOWED_TOOLS.includes("write"));
      assert.ok(!CURSOR_RUN_TOOLS.includes("write"));
      assert.ok(!CURSOR_RUN_DISALLOWED_TOOLS.includes("mcp"));
    });

    it("disables sandboxOptions by default", () => {
      const local = buildLocalAgentOptions({ cwd: "/tmp" });
      assert.equal(local.sandboxOptions.enabled, false);
      assert.equal(local.cwd, "/tmp");
      assert.deepEqual(local.settingSources, []);
      assert.equal(local.customTools, undefined);
    });

  it("passes SDK tool-name validation when @cursor/sdk is installed", async () => {
    let sdkModule = null;
    try {
      sdkModule = await import("@cursor/sdk");
    } catch {
      return;
    }

    await assert.doesNotReject(() => assertValidAgentToolOptions(sdkModule));
  });
});
