import { McpServer, type CallToolResult } from "@modelcontextprotocol/server";
import { z } from "zod";
import { readPreviewArtifact } from "./artifacts.js";
import type {
  InspectResult,
  ListTargetsResult,
  ReloadResult,
  ScreenshotResult,
  StatusResult,
} from "./protocol.js";
import { ProjectSession } from "./project-session.js";

const readOnlyAnnotations = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
  openWorldHint: false,
} as const;

export function createServer(): McpServer {
  const session = new ProjectSession();
  const server = new McpServer(
    { name: "ui-toolkit-mcp-server", version: "0.2.0" },
    {
      capabilities: { tools: {} },
      instructions:
        "Inspect and screenshot Unity UI Toolkit without entering Play Mode. First call ui_list_projects, then ui_select_project with one returned id. Next call ui_status and ui_list_targets. Unity must remain open with the target project loaded.",
    },
  );

  server.registerTool(
    "ui_list_projects",
    {
      title: "List Unity projects",
      description: "List open Unity projects that advertise a UI Toolkit preview endpoint.",
      inputSchema: z.object({}),
      annotations: readOnlyAnnotations,
    },
    async () => runTool(async () => textResult({ projects: await session.list() })),
  );

  server.registerTool(
    "ui_select_project",
    {
      title: "Select Unity project",
      description: "Select one discovered Unity project for subsequent preview tools in this MCP session.",
      inputSchema: z.object({
        projectId: z.string().min(1).describe("Project id returned by ui_list_projects."),
      }),
      annotations: readOnlyAnnotations,
    },
    async ({ projectId }) => runTool(async () => textResult({ project: await session.select(projectId) })),
  );

  server.registerTool(
    "ui_status",
    {
      title: "UI Toolkit preview status",
      description: "Check the connected Unity Editor and preview capabilities.",
      inputSchema: z.object({}),
      annotations: readOnlyAnnotations,
    },
    async () =>
      runTool(async () => {
        const { client } = await session.client();
        return textResult(await client.call<StatusResult>("status", {}));
      }),
  );

  server.registerTool(
    "ui_list_targets",
    {
      title: "List UI Toolkit targets",
      description: "List configured previews, UXML assets, and currently open UI Toolkit Editor windows.",
      inputSchema: z.object({
        query: z.string().optional().describe("Case-insensitive filter for id, name, path, type, or title."),
        includePackages: z.boolean().default(true).describe("Include UXML assets from Packages/."),
      }),
      annotations: readOnlyAnnotations,
    },
    async ({ query, includePackages }) =>
      runTool(async () => {
        const { client } = await session.client();
        return textResult(
          await client.call<ListTargetsResult>("list_targets", {
            ...(query === undefined ? {} : { query }),
            includePackages,
          }),
        );
      }),
  );

  server.registerTool(
    "ui_inspect",
    {
      title: "Inspect UI Toolkit layout",
      description: "Return the element tree, bounds, text/value fields, and optionally resolved styles for a target.",
      inputSchema: z.object({
        target: z.string().min(1).describe("Target id or project-relative .uxml asset path."),
        selector: z.string().optional().describe("Simple selector: :root, #name, .class, element name, or type."),
        depth: z.number().int().min(1).max(64).default(8),
        includeResolvedStyles: z.boolean().default(true),
        width: z.number().int().min(64).max(16384).default(1280),
        height: z.number().int().min(64).max(16384).default(720),
      }),
      annotations: readOnlyAnnotations,
    },
    async ({ target, selector, depth, includeResolvedStyles, width, height }) =>
      runTool(async () => {
        const { client } = await session.client();
        return textResult(
          await client.call<InspectResult>("inspect", {
            target: { id: target },
            ...(selector === undefined ? {} : { selector }),
            depth,
            includeResolvedStyles,
            width,
            height,
          }),
        );
      }),
  );

  server.registerTool(
    "ui_screenshot",
    {
      title: "Capture UI Toolkit screenshot",
      description: "Render a target or selected element to one or more PNG images. Use height='full' for scroll content.",
      inputSchema: z.object({
        target: z.string().min(1).describe("Target id or project-relative .uxml asset path."),
        selector: z.string().optional().describe("Optional element selector to crop around."),
        width: z.number().int().min(64).max(16384).default(1280),
        height: z.union([z.number().int().min(64).max(16384), z.literal("full")]).default(720),
        theme: z.enum(["editor-dark", "editor-light", "runtime"]).default("editor-dark"),
        background: z
          .union([z.literal("theme"), z.string().regex(/^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/)])
          .default("theme")
          .describe("Canvas background. Defaults to the selected theme; use #00000000 for transparency."),
      }),
      annotations: readOnlyAnnotations,
    },
    async ({ target, selector, width, height, theme, background }) =>
      runTool(async () => {
        const { project, client } = await session.client();
        const result = await client.call<ScreenshotResult>("screenshot", {
          target: { id: target },
          ...(selector === undefined ? {} : { selector }),
          width,
          height: height === "full" ? 720 : height,
          fullHeight: height === "full",
          theme,
          background,
        });
        const images = await Promise.all(
          result.artifacts.map(async (artifact) => ({
            type: "image" as const,
            data: (await readPreviewArtifact(project.path, artifact.path)).toString("base64"),
            mimeType: "image/png",
          })),
        );
        return {
          content: [
            {
              type: "text" as const,
              text: JSON.stringify(result, null, 2),
            },
            ...images,
          ],
          structuredContent: result as unknown as Record<string, unknown>,
        };
      }),
  );

  server.registerTool(
    "ui_reload",
    {
      title: "Reload UI Toolkit assets",
      description: "Refresh all assets or reimport selected project-relative .uxml/.uss paths, without modifying project files.",
      inputSchema: z.object({
        paths: z.array(z.string().min(1)).optional(),
      }),
      annotations: readOnlyAnnotations,
    },
    async ({ paths }) =>
      runTool(async () => {
        const { client } = await session.client();
        return textResult(await client.call<ReloadResult>("reload", paths === undefined ? {} : { paths }));
      }),
  );

  return server;
}

async function runTool(operation: () => Promise<CallToolResult>): Promise<CallToolResult> {
  try {
    return await operation();
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return { content: [{ type: "text", text: message }], isError: true };
  }
}

function textResult(value: object): CallToolResult {
  return {
    content: [{ type: "text", text: JSON.stringify(value, null, 2) }],
    structuredContent: value as Record<string, unknown>,
  };
}
