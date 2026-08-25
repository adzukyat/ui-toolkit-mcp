# UI Toolkit MCP Server

Node.js stdio MCP bridge for inspecting and capturing UI Toolkit in an open Unity Editor.

```bash
npx ui-toolkit-mcp-server mcp
```

The matching `me.adzuki.ui-toolkit-mcp.preview-server` Unity package must be installed in each project. It exposes seven local tools. Start with `ui_list_projects`, select one returned id with `ui_select_project`, then use `ui_status`, `ui_list_targets`, `ui_inspect`, `ui_screenshot`, and `ui_reload`.

See the source repository README for Unity package installation, preview configuration, security details, and limitations.
