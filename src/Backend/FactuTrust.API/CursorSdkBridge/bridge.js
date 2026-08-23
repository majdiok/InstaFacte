import http from "node:http";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  assertValidAgentToolOptions,
  buildLocalAgentOptions,
  CURSOR_RUN_DISALLOWED_TOOLS,
  CURSOR_RUN_TOOLS
} from "./bridge-agent-options.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const requestedPort = Number.parseInt(process.env.CURSOR_SDK_BRIDGE_PORT || "0", 10) || 0;

let sdkModule = null;
let sdkLoadError = null;
try {
  sdkModule = await import("@cursor/sdk");
} catch (err) {
  sdkLoadError = err?.message || String(err);
}

function sendJson(res, status, body) {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(payload)
  });
  res.end(payload);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on("data", (c) => chunks.push(c));
    req.on("end", () => {
      const raw = Buffer.concat(chunks).toString("utf8");
      if (!raw) {
        resolve({});
        return;
      }
      try {
        resolve(JSON.parse(raw));
      } catch (err) {
        reject(err);
      }
    });
    req.on("error", reject);
  });
}

function writeSse(res, event) {
  res.write(`data: ${JSON.stringify(event)}\n\n`);
}

function toModelSelection(model) {
  if (!model?.id) return undefined;
  const params = Array.isArray(model.params)
    ? model.params.map((p) => ({ id: p.id, value: String(p.value ?? "") }))
    : undefined;
  return params?.length ? { id: model.id, params } : { id: model.id };
}

function ensureScratch(dir) {
  const scratch = dir && dir.trim()
    ? dir
    : fs.mkdtempSync(path.join(os.tmpdir(), "ft-cursor-"));
  fs.mkdirSync(scratch, { recursive: true });
  return scratch;
}

async function callToolCallback(callbackUrl, token, name, args) {
  const response = await fetch(callbackUrl, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Cursor-Run-Token": token || ""
    },
    body: JSON.stringify({ name, arguments: args ?? {} })
  });
  const text = await response.text();
  if (!response.ok) {
    return { isError: true, content: [{ type: "text", text: text || `HTTP ${response.status}` }] };
  }
  try {
    const parsed = JSON.parse(text);
    if (typeof parsed === "string") return parsed;
    if (parsed?.content) return parsed;
    return JSON.stringify(parsed);
  } catch {
    return text;
  }
}

function buildCustomTools(tools, callbackUrl, token) {
  const customTools = {};
  for (const tool of tools || []) {
    if (!tool?.name) continue;
    customTools[tool.name] = {
      description: tool.description || "",
      inputSchema: tool.inputSchema || { type: "object", properties: {} },
      async execute(args) {
        return callToolCallback(callbackUrl, token, tool.name, args);
      }
    };
  }
  return customTools;
}

function normalizeStreamEvent(event) {
  if (!event || typeof event !== "object") return null;
  if (event.type === "assistant") {
    const blocks = event.message?.content || [];
    const text = blocks.filter((b) => b?.type === "text").map((b) => b.text || "").join("");
    if (!text) return null;
    return { type: "assistant_text", text };
  }
  if (event.type === "tool_call") {
    const status = event.status || "";
    const name = event.name || event.toolCall?.name || "";
    const id = event.toolCallId || event.id || "";
    if (status === "completed" || status === "error") {
      return { type: "tool_call_end", toolName: name, toolCallId: id, status };
    }
    return { type: "tool_call_start", toolName: name, toolCallId: id, status };
  }
  if (event.type === "status") {
    return { type: "status", status: event.status || "" };
  }
  return null;
}

async function handleModels(req, res) {
  if (!sdkModule?.Cursor) {
    sendJson(res, 503, { error: sdkLoadError || "@cursor/sdk indisponible" });
    return;
  }
  const body = await readBody(req);
  const models = await sdkModule.Cursor.models.list({ apiKey: body.apiKey });
  sendJson(res, 200, { models: models || [] });
}

async function handleExtract(req, res) {
  if (!sdkModule?.Agent) {
    sendJson(res, 503, { error: sdkLoadError || "@cursor/sdk indisponible" });
    return;
  }
  const body = await readBody(req);
  const scratch = ensureScratch(body.scratchDirectory);
  const system = body.systemPrompt || "";
  const user = body.userText || "";
  const jsonHint =
    "\n\nRéponds UNIQUEMENT avec un objet JSON valide, sans markdown ni commentaire.";
  const text = `${system}${jsonHint}\n\n${user}`;
  const images = Array.isArray(body.images) ? body.images : [];
  const message = images.length
    ? { text, images: images.map((img) => ({ data: img.data, mimeType: img.mimeType || "image/png" })) }
    : text;

    const result = await sdkModule.Agent.prompt(message, {
    apiKey: body.apiKey,
    model: toModelSelection(body.model),
    local: buildLocalAgentOptions({ cwd: scratch }),
    tools: []
  });

  if (result.status === "error") {
    sendJson(res, 502, { error: result.error?.message || "run failed", status: result.status });
    return;
  }
  sendJson(res, 200, { text: result.result || "", status: result.status });
}

async function handleRuns(req, res) {
  if (!sdkModule?.Agent) {
    sendJson(res, 503, { error: sdkLoadError || "@cursor/sdk indisponible" });
    return;
  }
  const body = await readBody(req);
  const scratch = ensureScratch(body.scratchDirectory);
  const customTools = buildCustomTools(body.tools, body.callbackUrl, body.callbackToken);
  const images = Array.isArray(body.images) ? body.images : [];
  const text = body.userText || "";
  const message = images.length
    ? { text, images: images.map((img) => ({ data: img.data, mimeType: img.mimeType || "image/png" })) }
    : text;

  res.writeHead(200, {
    "Content-Type": "text/event-stream; charset=utf-8",
    "Cache-Control": "no-cache",
    Connection: "keep-alive"
  });

  let agent;
  try {
    agent = await sdkModule.Agent.create({
      apiKey: body.apiKey,
      model: toModelSelection(body.model),
      tools: CURSOR_RUN_TOOLS,
      disallowedTools: CURSOR_RUN_DISALLOWED_TOOLS,
      local: buildLocalAgentOptions({ cwd: scratch, customTools })
    });

    const run = await agent.send(message);
    writeSse(res, { type: "started", agentId: agent.agentId, runId: run.id });

    const abort = () => {
      try {
        if (run.supports?.("cancel")) run.cancel();
      } catch {
        /* ignore */
      }
    };
    req.on("close", abort);

    for await (const event of run.stream()) {
      const normalized = normalizeStreamEvent(event);
      if (normalized) writeSse(res, normalized);
    }

    const result = await run.wait();
    if (result.status === "error") {
      writeSse(res, {
        type: "error",
        error: result.error?.message || "run failed",
        status: result.status
      });
    } else {
      writeSse(res, {
        type: "done",
        status: result.status,
        text: result.result || "",
        agentId: agent.agentId,
        runId: run.id
      });
    }
  } catch (err) {
    writeSse(res, {
      type: "error",
      error: err?.message || String(err),
      isRetryable: Boolean(err?.isRetryable)
    });
  } finally {
    try {
      if (agent?.[Symbol.asyncDispose]) await agent[Symbol.asyncDispose]();
      else agent?.close?.();
    } catch {
      /* ignore */
    }
    res.end();
  }
}

const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url || "/", "http://127.0.0.1");
    if (req.method === "GET" && url.pathname === "/health") {
      sendJson(res, 200, {
        ok: true,
        sdk: Boolean(sdkModule),
        sdkError: sdkLoadError,
        node: process.version
      });
      return;
    }
    if (req.method === "POST" && url.pathname === "/models") {
      await handleModels(req, res);
      return;
    }
    if (req.method === "POST" && url.pathname === "/extract") {
      await handleExtract(req, res);
      return;
    }
    if (req.method === "POST" && url.pathname === "/runs") {
      await handleRuns(req, res);
      return;
    }
    sendJson(res, 404, { error: "not found" });
  } catch (err) {
    if (!res.headersSent) {
      sendJson(res, 500, { error: err?.message || String(err) });
    } else {
      res.end();
    }
  }
});

try {
  await assertValidAgentToolOptions(sdkModule);
} catch (err) {
  console.error(err?.message || err);
  process.exit(1);
}

server.listen(requestedPort, "127.0.0.1", () => {
  const addr = server.address();
  process.stdout.write(`READY ${addr.port}\n`);
});
