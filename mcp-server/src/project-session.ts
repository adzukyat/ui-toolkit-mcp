import { discoverUnityProjects, type UnityProjectInfo } from "./project.js";
import { UnityClient } from "./unity-client.js";

export interface ListedUnityProject extends UnityProjectInfo {
  selected: boolean;
}

export class ProjectSession {
  private selectedProjectId: string | undefined;

  constructor(private readonly discover: () => Promise<UnityProjectInfo[]> = discoverUnityProjects) {}

  async list(): Promise<ListedUnityProject[]> {
    return (await this.discover()).map((project) => ({
      ...project,
      selected: project.id === this.selectedProjectId,
    }));
  }

  async select(id: string): Promise<ListedUnityProject> {
    const project = (await this.discover()).find((candidate) => candidate.id === id);
    if (project === undefined) {
      throw new Error(`Unity project '${id}' is unavailable. Call ui_list_projects to refresh the available project ids.`);
    }
    this.selectedProjectId = project.id;
    return { ...project, selected: true };
  }

  async selected(): Promise<UnityProjectInfo> {
    if (this.selectedProjectId === undefined) {
      throw new Error("No Unity project is selected. Call ui_list_projects, then ui_select_project.");
    }
    const project = (await this.discover()).find((candidate) => candidate.id === this.selectedProjectId);
    if (project === undefined) {
      throw new Error("The selected Unity project is no longer available. Call ui_list_projects, then ui_select_project again.");
    }
    return project;
  }

  async client(): Promise<{ project: UnityProjectInfo; client: UnityClient }> {
    const project = await this.selected();
    return { project, client: new UnityClient(project.path) };
  }
}
