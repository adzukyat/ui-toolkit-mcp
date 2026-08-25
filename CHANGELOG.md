# Changelog

## 0.2.0 - 2026-08-25

- Added discovery for all open Unity projects and session-local selection by opaque project id.
- Removed direct Unity project selection from MCP client configuration and CLI startup options.
- Kept access tokens out of the shared discovery directory.

## 0.1.0 - 2026-08-25

- Initial Unity package and stdio MCP bridge.
- Added `ui_status`, `ui_list_targets`, `ui_inspect`, `ui_screenshot`, and `ui_reload`.
- Added loopback token authentication, project identity validation, and artifact path confinement.
- Added Node tests, Unity EditMode fixtures, and MCP smoke validation.
