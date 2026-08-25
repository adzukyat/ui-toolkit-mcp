import { createHash } from "node:crypto";
import { readFile, readdir, realpath } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import type { EndpointDescriptor } from "./protocol.js";
import { PROTOCOL_VERSION } from "./protocol.js";

const ENDPOINT_RELATIVE_PATH = path.join("Library", "UIToolkitMcpPreviewServer", "endpoint.json");

interface DiscoveryDescriptor {
  schemaVersion: number;
  protocolVersion: number;
  processId: number;
  projectPath: string;
  projectName: string;
  endpointPath: string;
  unityVersion: string;
  startedAtUtc: string;
}

export interface UnityProjectInfo {
  id: string;
  name: string;
  path: string;
  unityVersion: string;
  processId: number;
  startedAtUtc: string;
}

export function discoveryDirectory(): string {
  const override = process.env.UI_TOOLKIT_MCP_DISCOVERY_DIR;
  if (override) return path.resolve(override);
  if (process.platform === "win32") {
    return path.join(process.env.LOCALAPPDATA ?? path.join(os.homedir(), "AppData", "Local"), "UIToolkitMcpPreviewServer", "endpoints");
  }
  return path.join(process.env.XDG_DATA_HOME ?? path.join(os.homedir(), ".local", "share"), "UIToolkitMcpPreviewServer", "endpoints");
}

export async function discoverUnityProjects(directory = discoveryDirectory()): Promise<UnityProjectInfo[]> {
  let entries: string[];
  try {
    entries = await readdir(directory);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") return [];
    throw error;
  }

  const discovered = await Promise.all(
    entries.filter((entry) => entry.endsWith(".json")).map((entry) => readDiscoveryEntry(path.join(directory, entry))),
  );
  const unique = new Map<string, UnityProjectInfo>();
  for (const project of discovered) {
    if (project !== undefined) unique.set(project.id, project);
  }
  return [...unique.values()].sort((left, right) =>
    left.name.localeCompare(right.name) || left.path.localeCompare(right.path),
  );
}

export async function readEndpoint(projectPath: string): Promise<EndpointDescriptor> {
  const endpointPath = path.join(projectPath, ENDPOINT_RELATIVE_PATH);
  let raw: string;
  try {
    raw = await readFile(endpointPath, "utf8");
  } catch (error) {
    throw new Error(
      `Unity preview endpoint is unavailable at ${endpointPath}. Open this project in Unity and wait for script compilation to finish.`,
      { cause: error },
    );
  }

  let endpoint: EndpointDescriptor;
  try {
    endpoint = JSON.parse(raw) as EndpointDescriptor;
  } catch (error) {
    throw new Error(`Unity preview endpoint is not valid JSON: ${endpointPath}`, { cause: error });
  }

  validateEndpoint(endpoint);
  const advertisedProject = await canonicalPath(endpoint.projectPath);
  const actualProject = await realpath(projectPath);
  if (advertisedProject !== actualProject) {
    throw new Error(`Endpoint belongs to another Unity project (${advertisedProject}); expected ${actualProject}.`);
  }
  assertRunning(endpoint.processId);
  return endpoint;
}

export function projectId(projectPath: string): string {
  return `project:${createHash("sha256").update(projectPath).digest("hex").slice(0, 16)}`;
}

async function readDiscoveryEntry(descriptorPath: string): Promise<UnityProjectInfo | undefined> {
  try {
    const descriptor = JSON.parse(await readFile(descriptorPath, "utf8")) as DiscoveryDescriptor;
    if (descriptor.schemaVersion !== 1 || descriptor.protocolVersion !== PROTOCOL_VERSION) return undefined;
    if (!Number.isInteger(descriptor.processId) || descriptor.processId < 1) return undefined;
    if (typeof descriptor.projectPath !== "string" || typeof descriptor.endpointPath !== "string") return undefined;
    assertRunning(descriptor.processId);

    const projectPath = await realpath(descriptor.projectPath);
    const expectedEndpointPath = await realpath(path.join(projectPath, ENDPOINT_RELATIVE_PATH));
    const advertisedEndpointPath = await realpath(descriptor.endpointPath);
    if (expectedEndpointPath !== advertisedEndpointPath) return undefined;

    const endpoint = await readEndpoint(projectPath);
    if (endpoint.processId !== descriptor.processId) return undefined;
    const fallbackName = path.basename(projectPath);
    return {
      id: projectId(projectPath),
      name: typeof descriptor.projectName === "string" && descriptor.projectName.length > 0 ? descriptor.projectName : fallbackName,
      path: projectPath,
      unityVersion: typeof descriptor.unityVersion === "string" ? descriptor.unityVersion : endpoint.unityVersion,
      processId: descriptor.processId,
      startedAtUtc: typeof descriptor.startedAtUtc === "string" ? descriptor.startedAtUtc : endpoint.startedAtUtc,
    };
  } catch {
    return undefined;
  }
}

function validateEndpoint(endpoint: EndpointDescriptor): void {
  if (endpoint.protocolVersion !== PROTOCOL_VERSION) {
    throw new Error(`Protocol mismatch: MCP supports ${PROTOCOL_VERSION}, Unity advertises ${String(endpoint.protocolVersion)}.`);
  }
  if (!Number.isInteger(endpoint.port) || endpoint.port < 1 || endpoint.port > 65535) {
    throw new Error("Unity preview endpoint contains an invalid TCP port.");
  }
  if (!Number.isInteger(endpoint.processId) || endpoint.processId < 1) {
    throw new Error("Unity preview endpoint contains an invalid process id.");
  }
  if (typeof endpoint.token !== "string" || endpoint.token.length < 32) {
    throw new Error("Unity preview endpoint contains an invalid access token.");
  }
}

async function canonicalPath(candidate: string): Promise<string> {
  return realpath(candidate).catch(() => path.resolve(candidate));
}

function assertRunning(processId: number): void {
  try {
    process.kill(processId, 0);
  } catch (error) {
    throw new Error("The Unity endpoint is stale because its Editor process is no longer running.", { cause: error });
  }
}
