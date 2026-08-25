export const PROTOCOL_VERSION = 1;

export interface EndpointDescriptor {
  protocolVersion: number;
  port: number;
  processId: number;
  projectPath: string;
  unityVersion: string;
  token: string;
  startedAtUtc: string;
}

export interface ProtocolErrorData {
  code: string;
  message: string;
  details?: string;
}

export interface ResponseEnvelope {
  id: string | null;
  ok: boolean;
  payload?: string;
  error?: ProtocolErrorData;
}

export interface TargetReference {
  id: string;
}

export interface TargetInfo {
  kind: string;
  id: string;
  name?: string;
  path?: string;
  type?: string;
  title?: string;
  editorOnly: boolean;
  configured: boolean;
}

export interface StatusResult {
  protocolVersion: number;
  projectPath: string;
  unityVersion: string;
  processId: number;
  isCompiling: boolean;
  isUpdating: boolean;
  capabilities: string[];
  editorRenderer?: string;
  warnings: string[];
}

export interface ListTargetsResult {
  targets: TargetInfo[];
  warnings: string[];
}

export interface InspectResult {
  schemaVersion: number;
  target: TargetInfo;
  viewportWidth: number;
  viewportHeight: number;
  selector?: string;
  root: Record<string, unknown>;
  warnings: string[];
}

export interface ScreenshotArtifact {
  path: string;
  width: number;
  height: number;
  offsetY: number;
  mimeType: string;
}

export interface ScreenshotResult {
  schemaVersion: number;
  target: TargetInfo;
  artifacts: ScreenshotArtifact[];
  contentWidth: number;
  contentHeight: number;
  tiled: boolean;
  selector?: string;
  warnings: string[];
}

export interface ReloadResult {
  importedPaths: string[];
  refreshedAll: boolean;
}
