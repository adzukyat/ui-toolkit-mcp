#!/usr/bin/env node
import { serveStdio } from "@modelcontextprotocol/server/stdio";
import { runCli } from "./cli.js";
import { createServer } from "./server.js";

async function main(): Promise<void> {
  const args = process.argv.slice(2);
  const command = args.shift() ?? "mcp";

  if (command === "mcp") {
    const handle = serveStdio(() => createServer(), {
      onerror: (error) => process.stderr.write(`[ui-toolkit-mcp-server] ${error.message}\n`),
    });
    const close = async (): Promise<void> => {
      await handle.close();
      process.exit(0);
    };
    process.once("SIGINT", close);
    process.once("SIGTERM", close);
    return;
  }

  await runCli(command, args);
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);
  process.stderr.write(`[ui-toolkit-mcp-server] ${message}\n`);
  process.exitCode = 1;
});
