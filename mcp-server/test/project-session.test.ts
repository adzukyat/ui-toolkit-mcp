import { describe, expect, it } from "vitest";
import { ProjectSession } from "../src/project-session.js";
import type { UnityProjectInfo } from "../src/project.js";

const project: UnityProjectInfo = {
  id: "project:0123456789abcdef",
  name: "Example Project",
  path: "/example/project",
  unityVersion: "6000.3.22f1",
  processId: 123,
  startedAtUtc: "2026-08-25T00:00:00.000Z",
};

describe("ProjectSession", () => {
  it("requires explicit selection and marks the selected project", async () => {
    const session = new ProjectSession(async () => [project]);

    await expect(session.selected()).rejects.toThrow("ui_list_projects");
    await expect(session.list()).resolves.toEqual([{ ...project, selected: false }]);
    await expect(session.select(project.id)).resolves.toEqual({ ...project, selected: true });
    await expect(session.list()).resolves.toEqual([{ ...project, selected: true }]);
  });

  it("rejects an unavailable project id", async () => {
    const session = new ProjectSession(async () => [project]);

    await expect(session.select("project:missing")).rejects.toThrow("ui_list_projects");
  });
});
