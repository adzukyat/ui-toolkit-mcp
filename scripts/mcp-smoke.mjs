#!/usr/bin/env node
import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import process from "node:process";

const requestedProjectId = process.argv[2];

const child = spawn(
  process.execPath,
  [new URL("../mcp-server/dist/index.js", import.meta.url).pathname, "mcp"],
  { stdio: ["pipe", "pipe", "inherit"] },
);
const lines = createInterface({ input: child.stdout });
const pending = new Map();
lines.on("line", (line) => {
  const message = JSON.parse(line);
  if (message.id !== undefined && pending.has(message.id)) {
    pending.get(message.id)(message);
    pending.delete(message.id);
  }
});

let nextId = 1;
function request(method, params) {
  const id = nextId++;
  child.stdin.write(`${JSON.stringify({ jsonrpc: "2.0", id, method, params })}\n`);
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => {
      pending.delete(id);
      reject(new Error(`MCP request timed out: ${method}`));
    }, 40_000);
    pending.set(id, (message) => {
      clearTimeout(timeout);
      if (message.error) reject(new Error(`${method}: ${message.error.message}`));
      else resolve(message.result);
    });
  });
}

try {
  await request("initialize", {
    protocolVersion: "2025-11-25",
    capabilities: {},
    clientInfo: { name: "ui-toolkit-mcp-smoke", version: "0.1.0" },
  });
  child.stdin.write(`${JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" })}\n`);
  const listed = await request("tools/list", {});
  const names = listed.tools.map((tool) => tool.name).sort();
  const expected = [
    "ui_inspect",
    "ui_list_projects",
    "ui_list_targets",
    "ui_reload",
    "ui_screenshot",
    "ui_select_project",
    "ui_status",
  ];
  if (JSON.stringify(names) !== JSON.stringify(expected)) {
    throw new Error(`Unexpected MCP tools: ${names.join(", ")}`);
  }
  const unselectedStatus = await request("tools/call", { name: "ui_status", arguments: {} });
  if (!unselectedStatus.isError || !unselectedStatus.content?.[0]?.text?.includes("ui_select_project")) {
    throw new Error("ui_status did not require explicit project selection.");
  }
  const projectResult = await request("tools/call", { name: "ui_list_projects", arguments: {} });
  const projects = projectResult.structuredContent?.projects ?? [];
  const project = requestedProjectId
    ? projects.find((candidate) => candidate.id === requestedProjectId)
    : projects[0];
  if (!project) {
    throw new Error(
      requestedProjectId
        ? `Requested project id is unavailable: ${requestedProjectId}`
        : "ui_list_projects returned no open Unity projects.",
    );
  }
  const selection = await request("tools/call", {
    name: "ui_select_project",
    arguments: { projectId: project.id },
  });
  if (selection.isError || selection.structuredContent?.project?.id !== project.id) {
    throw new Error("ui_select_project did not select the discovered project.");
  }
  const status = await request("tools/call", { name: "ui_status", arguments: {} });
  if (status.isError || status.structuredContent?.protocolVersion !== 1) {
    throw new Error("ui_status did not return protocol version 1.");
  }
  const targetResult = await request("tools/call", {
    name: "ui_list_targets",
    arguments: { includePackages: true },
  });
  const target = targetResult.structuredContent?.targets?.find((item) => item.kind !== "window");
  let screenshotVerified = false;
  if (target) {
    const screenshot = await request("tools/call", {
      name: "ui_screenshot",
      arguments: { target: target.id, width: 320, height: 240, theme: "editor-dark" },
    });
    screenshotVerified = screenshot.content?.some(
      (item) => item.type === "image" && item.mimeType === "image/png" && item.data.startsWith("iVBOR"),
    );
    if (!screenshotVerified) throw new Error("ui_screenshot did not return PNG image content.");
  }
  process.stdout.write(
    `MCP smoke passed: ${names.join(", ")}${screenshotVerified ? "; PNG image content verified" : ""}\n`,
  );
} finally {
  lines.close();
  child.kill("SIGTERM");
}
