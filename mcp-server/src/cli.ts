import type {
  InspectResult,
  ListTargetsResult,
  ReloadResult,
  ScreenshotResult,
  StatusResult,
} from "./protocol.js";
import { discoverUnityProjects } from "./project.js";
import { UnityClient } from "./unity-client.js";

export async function runCli(command: string, args: string[]): Promise<void> {
  if (command === "projects") {
    print({ projects: await discoverUnityProjects() });
    return;
  }

  const projectCommands = new Set(["doctor", "status", "list", "inspect", "screenshot", "reload"]);
  if (!projectCommands.has(command)) {
    throw new Error(
      `Unknown command '${command}'. Use mcp, projects, doctor <project-id>, status <project-id>, list <project-id> [query], inspect <project-id> <target> [selector], screenshot <project-id> <target> [--full], or reload <project-id> [paths...].`,
    );
  }

  const projectId = required(args.shift(), `${command} requires a project id from the projects command.`);
  const project = (await discoverUnityProjects()).find((candidate) => candidate.id === projectId);
  if (project === undefined) {
    throw new Error(`Unity project '${projectId}' is unavailable. Run the projects command to refresh project ids.`);
  }
  const client = new UnityClient(project.path);
  switch (command) {
    case "doctor":
    case "status":
      print(await client.call<StatusResult>("status", {}));
      return;
    case "list":
      print(await client.call<ListTargetsResult>("list_targets", { query: args[0] ?? "", includePackages: true }));
      return;
    case "inspect": {
      const target = required(args[0], "inspect requires a target id or .uxml path.");
      print(
        await client.call<InspectResult>("inspect", {
          target: { id: target },
          ...(args[1] === undefined ? {} : { selector: args[1] }),
          depth: 8,
          includeResolvedStyles: true,
          width: 1280,
          height: 720,
        }),
      );
      return;
    }
    case "screenshot": {
      const target = required(args[0], "screenshot requires a target id or .uxml path.");
      const fullHeight = args.includes("--full");
      print(
        await client.call<ScreenshotResult>("screenshot", {
          target: { id: target },
          width: 1280,
          height: 720,
          fullHeight,
          theme: "editor-dark",
          background: "theme",
        }),
      );
      return;
    }
    case "reload":
      print(await client.call<ReloadResult>("reload", args.length === 0 ? {} : { paths: args }));
      return;
  }
}

function required(value: string | undefined, message: string): string {
  if (value === undefined || value.length === 0) throw new Error(message);
  return value;
}

function print(value: object): void {
  process.stdout.write(`${JSON.stringify(value, null, 2)}\n`);
}
