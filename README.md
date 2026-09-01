# UI Toolkit MCP

An MCP server that lets AI agents inspect UI Toolkit screens while the Unity Editor remains open. It can examine the structure and appearance of UXML/USS files and capture screenshots without entering Play Mode or building a Player.

Key features:

- List UXML files and open UI Toolkit windows
- Inspect element trees, text, values, positions, sizes, and resolved styles
- Report elements that extend beyond their parent or the preview area
- Capture an entire screen or a selected element as a PNG image
- Capture several widths in one request
- Automatically split long screens into tiles (up to 8192 px per image and 100 MP in total)
- Reimport UXML/USS files
- Compatibility tested with Unity 2022.3 LTS and Unity 6.3

## Setup

### 1. Add the Unity package

In the Unity Editor, open `Window > Package Manager`, click the `+` button in the upper-left corner, and select `Add package from git URL...`.

Enter the following URL:

```text
https://github.com/adzukyat/ui-toolkit-mcp.git?path=/unity-package
```

The preview server starts automatically after Unity finishes script compilation. Keep the Unity Editor open while using it.

### 2. Register the MCP server

Node.js 20 or later is required. Example configuration for Codex:

```toml
[mcp_servers.ui_toolkit_mcp]
command = "npx"
args = [
  "-y",
  "ui-toolkit-mcp-server@latest",
  "mcp"
]
```

## MCP tools

| Tool | Purpose |
| --- | --- |
| `ui_list_projects` | List open Unity projects that have the package installed |
| `ui_select_project` | Select the Unity project to work with |
| `ui_status` | Check the Unity version and connection status |
| `ui_list_targets` | List preview configurations, UXML files, and open `EditorWindow` instances |
| `ui_inspect` | Inspect the structure, position, size, and styles of UI elements |
| `ui_screenshot` | Capture the UI as a PNG image |
| `ui_reload` | Reload UXML/USS changes in Unity |

Start with `ui_list_projects` to find a project, then select it with `ui_select_project`. Next, use `ui_list_targets` to find the screen you want to inspect, followed by `ui_inspect` or `ui_screenshot`.

## Preview configuration (optional)

Place a `.ui-toolkit-mcp-preview.json` file in the project root to define stable aliases, additional USS files, themes, and viewports. Copy and adapt the following example:

```json
{
  "$schema": "./Packages/me.adzuki.ui-toolkit-mcp.preview-server/Schemas/ui-toolkit-mcp-preview.schema.json",
  "schemaVersion": 1,
  "previews": [
    {
      "alias": "settings",
      "document": "Assets/UI/Settings.uxml",
      "stylesheets": ["Assets/UI/Settings.uss"],
      "theme": "editor-dark",
      "background": "#383838",
      "selector": "#settings-root",
      "viewport": { "widths": [360, 480], "height": "full" },
      "state": {
        "#advanced-toggle": { "value": true },
        "#advanced-fields": { "display": true },
        "#mode-custom": { "addClasses": ["selected"] },
        "#name": { "value": "Example" }
      }
    }
  ]
}
```

Supported themes are `editor-dark`, `editor-light`, and `runtime`. `background` accepts `theme`, `#RRGGBB`, or `#RRGGBBAA`. A `panelSettings` asset path can also be specified for the runtime theme.

Use `viewport.width` for one width or `viewport.widths` for several. `state` is keyed by element selector. Each element can set `value`, `selectedIndex`, `text`, `display`, `visible`, `enabled`, `addClasses`, and `removeClasses`. Existing configurations do not need changes.

`ui_inspect` returns `overflows` with the element and parent bounds, plus the amount outside each edge. `ui_screenshot` also accepts one width or an array such as `[360, 768, 1280]`.

Screenshots use the selected theme's standard canvas color by default (`#383838` for Editor Dark and `#C8C8C8` for Editor Light). Pass `#00000000` as `background` when a transparent PNG is required. Runtime previews remain transparent unless the UI itself paints a background.

### When C# fills the screen

A UXML target loads the UXML and USS only. It does not call an `EditorWindow`'s `CreateGUI` or other setup code. The target list marks these entries as `uxml-only`; open windows are marked as `live`.

If C# adds icons, text, or child elements, use either the open window target or a parent UXML that creates the custom control. Use `state` for values and visual states that do not require C# to create new elements.

## Development and verification

```bash
npm test
npm run typecheck
npm run build
node scripts/mcp-smoke.mjs [project-id]
```

Unity EditMode tests are located in `test-projects/2022.3` and `test-projects/6000.3`. Pixel-accurate verification requires a graphics device. Tree inspection works with `-nographics`, but PNG rendering does not, so perform final verification in a normally launched Unity Editor.

## Current limitations

- The Editor offscreen renderer calls internal UI Toolkit APIs through reflection. `ui_status` reports a warning for unsupported Unity versions.
- Open-window IDs are valid only for the current Unity session.
- Pseudo-state injection such as hover and focus, pointer/keyboard interaction, and fixed animation time are not yet supported.
- Fonts, icons, custom controls, and data bindings are rendered only when they can be resolved in the target project.
