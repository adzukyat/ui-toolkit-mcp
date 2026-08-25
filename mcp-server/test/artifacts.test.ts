import { afterEach, describe, expect, it } from "vitest";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { readPreviewArtifact } from "../src/artifacts.js";

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map((directory) => rm(directory, { recursive: true, force: true })));
});

describe("readPreviewArtifact", () => {
  it("reads PNGs only from the Unity preview cache", async () => {
    const project = await mkdtemp(path.join(os.tmpdir(), "ui-toolkit-artifact-test-"));
    temporaryDirectories.push(project);
    const artifacts = path.join(project, "Library", "UIToolkitMcpPreviewServer", "artifacts");
    await mkdir(artifacts, { recursive: true });
    const png = path.join(artifacts, "preview.png");
    await writeFile(png, Buffer.from([137, 80, 78, 71]));

    await expect(readPreviewArtifact(project, png)).resolves.toEqual(Buffer.from([137, 80, 78, 71]));
  });

  it("refuses paths outside the preview cache", async () => {
    const project = await mkdtemp(path.join(os.tmpdir(), "ui-toolkit-artifact-test-"));
    temporaryDirectories.push(project);
    await mkdir(path.join(project, "Library", "UIToolkitMcpPreviewServer", "artifacts"), { recursive: true });
    const outside = path.join(project, "outside.png");
    await writeFile(outside, Buffer.from([137, 80, 78, 71]));

    await expect(readPreviewArtifact(project, outside)).rejects.toThrow("outside its preview cache");
  });
});
