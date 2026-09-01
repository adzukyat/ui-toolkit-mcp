# UI Toolkit MCP Preview Server

Editor-only Unity package used by UI Toolkit MCP. It starts a token-authenticated loopback endpoint, enumerates UI Toolkit targets, inspects element trees, and renders PNG artifacts under `Library/UIToolkitMcpPreviewServer`.

The package does not modify UXML, USS, scenes, prefabs, or project settings. See the repository README for MCP setup and usage.

UXML targets do not run `EditorWindow` setup code. Use a live window target when C# creates part of the screen. Preview configuration can set values, display state, and classes for UXML-only previews.
