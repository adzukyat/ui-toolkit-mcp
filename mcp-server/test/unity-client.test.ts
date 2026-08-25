import { afterEach, describe, expect, it } from "vitest";
import { createServer, type Server } from "node:net";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { UnityClient } from "../src/unity-client.js";

const temporaryDirectories: string[] = [];
const servers: Server[] = [];

afterEach(async () => {
  await Promise.all(servers.splice(0).map((server) => new Promise<void>((resolve) => server.close(() => resolve()))));
  await Promise.all(temporaryDirectories.splice(0).map((directory) => rm(directory, { recursive: true, force: true })));
});

describe("UnityClient", () => {
  it("authenticates and decodes the nested Unity payload", async () => {
    const project = await createProject();
    const token = "a".repeat(64);
    const server = createServer((socket) => {
      let requestText = "";
      socket.on("data", (chunk) => {
        requestText += chunk.toString("utf8");
        const newline = requestText.indexOf("\n");
        if (newline < 0) return;
        const request = JSON.parse(requestText.slice(0, newline)) as {
          id: string;
          token: string;
          method: string;
          payload: string;
        };
        expect(request.token).toBe(token);
        expect(request.method).toBe("status");
        expect(JSON.parse(request.payload)).toEqual({});
        socket.end(
          `${JSON.stringify({ id: request.id, ok: true, payload: JSON.stringify({ unityVersion: "test" }) })}\n`,
        );
      });
    });
    servers.push(server);
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    const address = server.address();
    if (typeof address === "string" || address === null) throw new Error("Expected a TCP address.");
    await writeEndpoint(project, address.port, token);

    await expect(new UnityClient(project).call("status", {})).resolves.toEqual({ unityVersion: "test" });
  });

  it("surfaces protocol errors with the Unity message", async () => {
    const project = await createProject();
    const token = "b".repeat(64);
    const server = createServer((socket) => {
      socket.once("data", (chunk) => {
        const request = JSON.parse(chunk.toString("utf8").trim()) as { id: string };
        socket.end(
          `${JSON.stringify({ id: request.id, ok: false, error: { code: "unity_busy", message: "Retry shortly." } })}\n`,
        );
      });
    });
    servers.push(server);
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    const address = server.address();
    if (typeof address === "string" || address === null) throw new Error("Expected a TCP address.");
    await writeEndpoint(project, address.port, token);

    await expect(new UnityClient(project).call("status", {})).rejects.toMatchObject({
      code: "unity_busy",
      message: "Retry shortly.",
    });
  });
});

async function createProject(): Promise<string> {
  const project = await mkdtemp(path.join(os.tmpdir(), "ui-toolkit-mcp-test-"));
  temporaryDirectories.push(project);
  await mkdir(path.join(project, "ProjectSettings"), { recursive: true });
  await mkdir(path.join(project, "Library", "UIToolkitMcpPreviewServer"), { recursive: true });
  await writeFile(path.join(project, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: test\n");
  return project;
}

async function writeEndpoint(project: string, port: number, token: string): Promise<void> {
  await writeFile(
    path.join(project, "Library", "UIToolkitMcpPreviewServer", "endpoint.json"),
    JSON.stringify({
      protocolVersion: 1,
      port,
      processId: process.pid,
      projectPath: project,
      unityVersion: "test",
      token,
      startedAtUtc: new Date().toISOString(),
    }),
  );
}
