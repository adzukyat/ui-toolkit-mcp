using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UIToolkitMcpPreviewServer.Inspection;
using UIToolkitMcpPreviewServer.Protocol;
using UIToolkitMcpPreviewServer.Rendering;
using UIToolkitMcpPreviewServer.Targets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer
{
    internal static class PreviewService
    {
        private const long MaximumCapturePixels = 100_000_000L;
        private const int PreferredTileHeight = 8192;

        internal static StatusResult Status()
        {
            var editorSupported = EditorPanelSession.IsSupported(out var editorRenderer);
            var graphicsAvailable = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            var capabilities = new List<string> { "window-inspect" };
            if (graphicsAvailable)
            {
                capabilities.Add("runtime-render");
                capabilities.Add("window-render");
                capabilities.Add("full-height");
                capabilities.Add("tiled-png");
                if (editorSupported)
                    capabilities.Add("editor-render");
            }
            var warnings = new List<string>();
            if (!editorSupported)
                warnings.Add("Editor screenshots are unavailable in this Unity version.");
            if (!graphicsAvailable)
                warnings.Add("Unity is running without a graphics device; screenshot tools are unavailable. Start the Editor without -nographics.");
            return new StatusResult
            {
                protocolVersion = ProtocolVersion.Current,
                projectPath = ProjectPaths.Root,
                unityVersion = Application.unityVersion,
                processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                capabilities = capabilities.ToArray(),
                editorRenderer = editorRenderer,
                warnings = warnings.ToArray()
            };
        }

        internal static ListTargetsResult ListTargets(ListTargetsParameters parameters)
        {
            return TargetCatalog.List(parameters ?? new ListTargetsParameters());
        }

        internal static InspectResult Inspect(InspectParameters parameters)
        {
            Normalize(parameters);
            var target = TargetCatalog.Resolve(parameters.target);
            using (var session = CreateSession(target, parameters.width, parameters.height, "editor-dark"))
            {
                session.ValidateLayout();
                var selected = ElementInspector.Find(session.Root, parameters.selector);
                if (selected == null)
                    throw new InvalidOperationException($"Selector '{parameters.selector}' did not match any element.");
                return new InspectResult
                {
                    target = target.info,
                    viewportWidth = parameters.width,
                    viewportHeight = parameters.height,
                    selector = parameters.selector,
                    root = ElementInspector.Describe(selected, parameters.depth, parameters.includeResolvedStyles),
                    warnings = session.Warnings.ToArray()
                };
            }
        }

        internal static ScreenshotResult Screenshot(ScreenshotParameters parameters)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new NotSupportedException("Screenshots require a graphics device. Start Unity without -nographics.");
            Normalize(parameters);
            var target = TargetCatalog.Resolve(parameters.target);
            ApplyPreviewDefaults(parameters, target.preview);
            var warnings = new List<string>();
            var background = ResolveBackground(parameters.background, parameters.theme);

            using (var session = CreateSession(target, parameters.width, parameters.height, parameters.theme))
            using (var expander = new ScrollViewExpander())
            {
                session.ValidateLayout();
                var selector = string.IsNullOrEmpty(parameters.selector) ? target.preview?.selector : parameters.selector;
                var selected = ElementInspector.Find(session.Root, selector);
                if (selected == null)
                    throw new InvalidOperationException($"Selector '{selector}' did not match any element.");

                var contentHeight = parameters.height;
                if (parameters.fullHeight)
                {
                    expander.Expand(selected, session.ValidateLayout);
                    contentHeight = ScrollViewExpander.MeasureContentHeight(selected);
                }

                var selectedOriginY = Mathf.Max(0, Mathf.FloorToInt(selected.worldBound.yMin - session.Root.worldBound.yMin));
                var selectedOriginX = Mathf.Max(0, Mathf.FloorToInt(selected.worldBound.xMin - session.Root.worldBound.xMin));
                var selectedWidth = Mathf.Clamp(Mathf.CeilToInt(selected.worldBound.width), 1, parameters.width - Mathf.Min(selectedOriginX, parameters.width - 1));
                var selectedHeight = Mathf.Clamp(Mathf.CeilToInt(selected.worldBound.height), 1, parameters.height - Mathf.Min(selectedOriginY, parameters.height - 1));
                var captureWidth = string.IsNullOrEmpty(selector) ? parameters.width : selectedWidth;
                if (!parameters.fullHeight && !string.IsNullOrEmpty(selector))
                    contentHeight = selectedHeight;
                if ((long)captureWidth * contentHeight > MaximumCapturePixels)
                    throw new InvalidOperationException("The requested capture exceeds 100 megapixels. Use a narrower selector or viewport.");

                var tileLimit = Mathf.Min(PreferredTileHeight, SystemInfo.maxTextureSize);
                var artifacts = new List<ScreenshotArtifact>();
                if (!parameters.fullHeight && !string.IsNullOrEmpty(selector))
                {
                    var raw = session.CapturePng(parameters.width, parameters.height, 0, background);
                    var png = PngCropper.Crop(raw, selectedOriginX, selectedOriginY, selectedWidth, selectedHeight);
                    var path = ArtifactStore.WritePng(png);
                    artifacts.Add(new ScreenshotArtifact
                    {
                        path = path,
                        width = selectedWidth,
                        height = selectedHeight,
                        offsetY = 0
                    });
                }
                var offset = 0;
                while (artifacts.Count == 0 && offset < contentHeight)
                {
                    var tileHeight = Mathf.Min(tileLimit, contentHeight - offset);
                    var raw = session.CapturePng(parameters.width, tileHeight, selectedOriginY + offset, background);
                    var png = string.IsNullOrEmpty(selector)
                        ? raw
                        : PngCropper.Crop(raw, selectedOriginX, 0, selectedWidth, tileHeight);
                    var path = ArtifactStore.WritePng(png);
                    artifacts.Add(new ScreenshotArtifact
                    {
                        path = path,
                        width = string.IsNullOrEmpty(selector) ? parameters.width : selectedWidth,
                        height = tileHeight,
                        offsetY = offset
                    });
                    offset += tileHeight;
                }

                warnings.AddRange(session.Warnings);
                if (artifacts.Count > 1)
                    warnings.Add($"The full-height capture was split into {artifacts.Count} vertical tiles.");
                return new ScreenshotResult
                {
                    target = target.info,
                    artifacts = artifacts.ToArray(),
                    contentWidth = captureWidth,
                    contentHeight = contentHeight,
                    tiled = artifacts.Count > 1,
                    selector = selector,
                    warnings = warnings.ToArray()
                };
            }
        }

        internal static ReloadResult Reload(ReloadParameters parameters)
        {
            var imported = new List<string>();
            var paths = parameters?.paths;
            if (paths == null || paths.Length == 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return new ReloadResult { importedPaths = Array.Empty<string>(), refreshedAll = true };
            }

            foreach (var path in paths.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                var assetPath = path.Replace('\\', '/');
                if ((!assetPath.StartsWith("Assets/", StringComparison.Ordinal) && !assetPath.StartsWith("Packages/", StringComparison.Ordinal)) ||
                    assetPath.Contains("../") || Path.IsPathRooted(assetPath))
                    throw new ArgumentException($"Reload paths must be project-relative Assets/ or Packages/ paths: {path}");
                if (!assetPath.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase) && !assetPath.EndsWith(".uss", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Only .uxml and .uss assets can be reloaded: {path}");
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                imported.Add(assetPath);
            }
            return new ReloadResult { importedPaths = imported.ToArray(), refreshedAll = false };
        }

        private static IRenderSession CreateSession(ResolvedTarget target, int width, int height, string theme)
        {
            if (target.window != null)
                return new EditorWindowSession(target.window);
            var preview = target.preview ?? new PreviewDefinition { theme = theme };
            if (string.IsNullOrEmpty(preview.theme))
                preview.theme = theme;
            var editor = target.info.editorOnly || !string.Equals(preview.theme, "runtime", StringComparison.OrdinalIgnoreCase);
            if (editor)
                return new EditorPanelSession(target.document, preview, width, height);
            return new RuntimePanelSession(target.document, preview, width, height);
        }

        internal static void ApplyPreviewDefaults(ScreenshotParameters parameters, PreviewDefinition preview)
        {
            if (preview == null)
                return;
            if (string.IsNullOrEmpty(parameters.selector))
                parameters.selector = preview.selector;
            if (!string.IsNullOrEmpty(preview.theme) && parameters.theme == "editor-dark")
                parameters.theme = preview.theme;
            if (!string.IsNullOrEmpty(preview.background) && parameters.background == "theme")
                parameters.background = preview.background;
            if (preview.viewport != null)
            {
                if (preview.viewport.width >= 64 && parameters.width == 1280)
                    parameters.width = preview.viewport.width;
                if (string.Equals(preview.viewport.height, "full", StringComparison.OrdinalIgnoreCase) && parameters.height == 720)
                    parameters.fullHeight = true;
                else if (int.TryParse(preview.viewport.height, out var configuredHeight) && parameters.height == 720)
                    parameters.height = configuredHeight;
            }
        }

        private static void Normalize(InspectParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters.width = Mathf.Clamp(parameters.width <= 0 ? 1280 : parameters.width, 64, SystemInfo.maxTextureSize);
            parameters.height = Mathf.Clamp(parameters.height <= 0 ? 720 : parameters.height, 64, SystemInfo.maxTextureSize);
            parameters.depth = Mathf.Clamp(parameters.depth <= 0 ? 8 : parameters.depth, 1, 64);
        }

        private static void Normalize(ScreenshotParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters.width = Mathf.Clamp(parameters.width <= 0 ? 1280 : parameters.width, 64, SystemInfo.maxTextureSize);
            parameters.height = Mathf.Clamp(parameters.height <= 0 ? 720 : parameters.height, 64, SystemInfo.maxTextureSize);
            if (string.IsNullOrEmpty(parameters.theme))
                parameters.theme = "editor-dark";
            if (string.IsNullOrEmpty(parameters.background))
                parameters.background = "theme";
        }

        internal static Color ResolveBackground(string value, string theme)
        {
            if (string.Equals(value, "theme", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(theme, "editor-dark", StringComparison.OrdinalIgnoreCase))
                    return new Color32(56, 56, 56, 255);
                if (string.Equals(theme, "editor-light", StringComparison.OrdinalIgnoreCase))
                    return new Color32(200, 200, 200, 255);
                return Color.clear;
            }
            if (!string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value, out var color))
                return color;
            throw new ArgumentException($"Invalid background color '{value}'. Use theme, #RRGGBB, or #RRGGBBAA.");
        }
    }
}
