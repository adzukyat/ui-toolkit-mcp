import { readFile, realpath } from "node:fs/promises";
import path from "node:path";

export async function readPreviewArtifact(projectPath: string, artifactPath: string): Promise<Buffer> {
  const root = await realpath(path.join(projectPath, "Library", "UIToolkitMcpPreviewServer", "artifacts"));
  const artifact = await realpath(artifactPath);
  const relative = path.relative(root, artifact);
  if (relative === "" || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
    throw new Error("Unity returned an artifact outside its preview cache; refusing to read it.");
  }
  if (path.extname(artifact).toLowerCase() !== ".png") {
    throw new Error("Unity returned an artifact that is not a PNG file.");
  }
  return readFile(artifact);
}
