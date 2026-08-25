import { createConnection } from "node:net";
import { randomUUID } from "node:crypto";
import type { EndpointDescriptor, ResponseEnvelope } from "./protocol.js";
import { readEndpoint } from "./project.js";

const MAXIMUM_RESPONSE_BYTES = 128 * 1024 * 1024;
const REQUEST_TIMEOUT_MS = 35_000;

export class UnityProtocolError extends Error {
  readonly code: string;
  readonly details?: string;

  constructor(code: string, message: string, details?: string) {
    super(message);
    this.name = "UnityProtocolError";
    this.code = code;
    if (details !== undefined) {
      this.details = details;
    }
  }
}

export class UnityClient {
  constructor(readonly projectPath: string) {}

  async call<T>(method: string, payload: unknown): Promise<T> {
    const endpoint = await readEndpoint(this.projectPath);
    const id = randomUUID();
    const request = JSON.stringify({
      id,
      token: endpoint.token,
      method,
      payload: JSON.stringify(payload ?? {}),
    });
    const response = await exchange(endpoint, request);
    if (response.id !== id) {
      throw new UnityProtocolError("response_mismatch", "Unity returned a response for another request.");
    }
    if (!response.ok) {
      throw new UnityProtocolError(
        response.error?.code ?? "unity_error",
        response.error?.message ?? "Unity rejected the request.",
        response.error?.details,
      );
    }
    if (typeof response.payload !== "string") {
      throw new UnityProtocolError("invalid_response", "Unity returned no response payload.");
    }
    try {
      return JSON.parse(response.payload) as T;
    } catch (error) {
      throw new UnityProtocolError(
        "invalid_response",
        `Unity returned malformed JSON: ${error instanceof Error ? error.message : String(error)}`,
      );
    }
  }
}

function exchange(endpoint: EndpointDescriptor, request: string): Promise<ResponseEnvelope> {
  return new Promise((resolve, reject) => {
    const socket = createConnection({ host: "127.0.0.1", port: endpoint.port });
    let settled = false;
    let total = 0;
    const chunks: Buffer[] = [];

    const finishError = (error: Error): void => {
      if (settled) return;
      settled = true;
      socket.destroy();
      reject(error);
    };

    socket.setTimeout(REQUEST_TIMEOUT_MS, () => {
      finishError(new Error("Timed out waiting for the Unity Editor preview server."));
    });
    socket.on("error", (error) => {
      finishError(new Error(`Could not connect to the Unity Editor preview server: ${error.message}`, { cause: error }));
    });
    socket.on("connect", () => {
      socket.write(`${request}\n`, "utf8");
    });
    socket.on("data", (chunk: Buffer) => {
      total += chunk.length;
      if (total > MAXIMUM_RESPONSE_BYTES) {
        finishError(new Error("Unity response exceeded the 128 MiB safety limit."));
        return;
      }
      chunks.push(chunk);
      const buffer = Buffer.concat(chunks, total);
      const newline = buffer.indexOf(0x0a);
      if (newline < 0) return;

      if (settled) return;
      settled = true;
      socket.end();
      try {
        resolve(JSON.parse(buffer.subarray(0, newline).toString("utf8")) as ResponseEnvelope);
      } catch (error) {
        reject(new Error("Unity returned an invalid protocol response.", { cause: error }));
      }
    });
    socket.on("end", () => {
      if (!settled) {
        finishError(new Error("Unity closed the connection before returning a complete response."));
      }
    });
  });
}
