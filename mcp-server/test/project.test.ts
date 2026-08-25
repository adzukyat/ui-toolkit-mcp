import { afterEach, describe, expect, it } from "vitest";
import { mkdtemp, mkdir, realpath, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { discoverUnityProjects, projectId } from "../src/project.js";

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map((directory) => rm(directory, { recursive: true, force: true })));
});

describe("discoverUnityProjects", () => {
  it("discovers a live advertised Unity project without exposing its token", async () => {
    const { discovery, project, descriptorPath } = await createAdvertisedProject("Preview Fixture");

    const projects = await discoverUnityProjects(discovery);

    expect(projects).toEqual([
      {
        id: projectId(await realpath(project)),
        name: "Preview Fixture",
        path: await realpath(project),
        unityVersion: "6000.3.22f1",
        processId: process.pid,
        startedAtUtc: "2026-08-25T00:00:00.000Z",
      },
    ]);
    expect(await readText(descriptorPath)).not.toContain("test-access-token");
  });

  it("ignores stale and mismatched discovery entries", async () => {
    const fixture = await createAdvertisedProject("Ignored Fixture");
    const descriptor = JSON.parse(await readText(fixture.descriptorPath)) as Record<string, unknown>;
    descriptor.processId = 2147483647;
    await writeFile(fixture.descriptorPath, JSON.stringify(descriptor));

    await expect(discoverUnityProjects(fixture.discovery)).resolves.toEqual([]);
  });
});

async function createAdvertisedProject(name: string): Promise<{
  discovery: string;
  project: string;
  descriptorPath: string;
}> {
  const root = await mkdtemp(path.join(os.tmpdir(), "ui-toolkit-discovery-test-"));
  temporaryDirectories.push(root);
  const project = path.join(root, "UnityProject");
  const discovery = path.join(root, "discovery");
  const endpointPath = path.join(project, "Library", "UIToolkitMcpPreviewServer", "endpoint.json");
  const descriptorPath = path.join(discovery, `${process.pid}.json`);
  await mkdir(path.dirname(endpointPath), { recursive: true });
  await mkdir(discovery, { recursive: true });
  const startedAtUtc = "2026-08-25T00:00:00.000Z";
  await writeFile(
    endpointPath,
    JSON.stringify({
      protocolVersion: 1,
      port: 5199,
      processId: process.pid,
      projectPath: project,
      unityVersion: "6000.3.22f1",
      token: "test-access-token-that-is-long-enough-0001",
      startedAtUtc,
    }),
  );
  await writeFile(
    descriptorPath,
    JSON.stringify({
      schemaVersion: 1,
      protocolVersion: 1,
      processId: process.pid,
      projectPath: project,
      projectName: name,
      endpointPath,
      unityVersion: "6000.3.22f1",
      startedAtUtc,
    }),
  );
  return { discovery, project, descriptorPath };
}

async function readText(file: string): Promise<string> {
  const { readFile } = await import("node:fs/promises");
  return readFile(file, "utf8");
}
