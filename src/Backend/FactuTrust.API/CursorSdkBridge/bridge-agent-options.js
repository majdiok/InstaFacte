import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

/** Allowlist / denylist for chat runs — must use @cursor/sdk public tool names only. */
export const CURSOR_RUN_TOOLS = ["mcp"];

/**
 * Defense-in-depth exclusions. Never include "mcp" (would remove customTools).
 * "write" is not a valid public name in @cursor/sdk 1.0.27 — use "edit" instead.
 */
export const CURSOR_RUN_DISALLOWED_TOOLS = ["shell", "edit", "read", "task"];

const DISALLOWED_TOOLS_CONFIG_ERROR = "Unknown tool name(s) in disallowedTools";

/**
 * Fail-fast at bridge startup when disallowedTools contains invalid SDK names.
 * @param {{ Agent?: { create: (opts: object) => Promise<unknown> } } | null} sdkModule
 */
export async function assertValidAgentToolOptions(sdkModule) {
  if (!sdkModule?.Agent) {
    return;
  }

  const scratch = await fs.mkdtemp(path.join(os.tmpdir(), "ft-cursor-validate-"));
  try {
    await sdkModule.Agent.create({
      apiKey: "ft-cursor-tool-validation",
      model: { id: "composer-2.5" },
      tools: CURSOR_RUN_TOOLS,
      disallowedTools: CURSOR_RUN_DISALLOWED_TOOLS,
      local: {
        cwd: scratch,
        settingSources: [],
        sandboxOptions: { enabled: true },
        customTools: {}
      }
    });
  } catch (err) {
    const message = err?.message || String(err);
    if (message.includes(DISALLOWED_TOOLS_CONFIG_ERROR)) {
      throw new Error(`CursorSdkBridge: ${message}`);
    }
    // Auth/network errors mean tool names passed SDK validation.
  } finally {
    try {
      await fs.rm(scratch, { recursive: true, force: true });
    } catch {
      /* ignore */
    }
  }
}
