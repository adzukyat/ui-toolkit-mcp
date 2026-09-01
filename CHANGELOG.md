# Changelog

## 0.3.1 - 2026-09-01

- Fixed full-height captures of selected elements inside scroll views.
- Inspect selected scroll content after moving it into view so viewport overflow reports reflect the visible position.

## 0.3.0 - 2026-09-01

- Added preview state for values, selection, text, display, visibility, enabled state, and classes.
- Added responsive screenshots for several widths in one call.
- Added overflow reports to layout inspection.
- Fixed preview viewport defaults in inspection, selected elements inside scroll views, and live window capture coordinates.
- Marked targets as UXML-only or live so C# initialization requirements are clear.

## 0.2.0 - 2026-08-25

- Added discovery for all open Unity projects and session-local selection by opaque project id.
- Removed direct Unity project selection from MCP client configuration and CLI startup options.
- Kept access tokens out of the shared discovery directory.

## 0.1.0 - 2026-08-25

- Initial Unity package and stdio MCP bridge.
- Added `ui_status`, `ui_list_targets`, `ui_inspect`, `ui_screenshot`, and `ui_reload`.
- Added loopback token authentication, project identity validation, and artifact path confinement.
- Added Node tests, Unity EditMode fixtures, and MCP smoke validation.
