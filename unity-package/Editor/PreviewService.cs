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
            capabilities.Add("preview-state");
            capabilities.Add("overflow-report");
            if (graphicsAvailable)
            {
                capabilities.Add("runtime-render");
                capabilities.Add("window-render");
                capabilities.Add("full-height");
                capabilities.Add("tiled-png");
                capabilities.Add("multi-width");
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
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            var target = TargetCatalog.Resolve(parameters.target);
            ApplyPreviewDefaults(parameters, target.preview, target.window);
            Normalize(parameters);
            using (var session = CreateSession(target, parameters.width, parameters.height, "editor-dark"))
            using (var revealer = new ScrollViewRevealer())
            {
                var warnings = new List<string>(PreviewStateApplier.Apply(session.Root, target.preview?.state));
                session.ValidateLayout();
                var selected = ElementInspector.Find(session.Root, parameters.selector);
                if (selected == null)
                    throw new InvalidOperationException($"Selector '{parameters.selector}' did not match any element.");
                if (!string.IsNullOrEmpty(parameters.selector))
                    revealer.Reveal(selected, session.ValidateLayout);
                session.ValidateLayout();
                return new InspectResult
                {
                    target = target.info,
                    viewportWidth = parameters.width,
                    viewportHeight = parameters.height,
                    selector = parameters.selector,
                    root = ElementInspector.Describe(selected, parameters.depth, parameters.includeResolvedStyles),
                    overflows = ElementInspector.FindOverflows(selected, session.ViewportBounds),
                    warnings = warnings.Concat(session.Warnings).ToArray()
                };
            }
        }

        internal static ScreenshotResult Screenshot(ScreenshotParameters parameters)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new NotSupportedException("Screenshots require a graphics device. Start Unity without -nographics.");
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            var target = TargetCatalog.Resolve(parameters.target);
            ApplyPreviewDefaults(parameters, target.preview, target.window);
            var captures = new List<ScreenshotCapture>();
            var warnings = new List<string>();
            var selector = parameters.selector;
            foreach (var width in ResolveWidths(parameters))
            {
                var captureParameters = new ScreenshotParameters
                {
                    target = parameters.target,
                    selector = parameters.selector,
                    width = width,
                    height = parameters.height,
                    fullHeight = parameters.fullHeight,
                    theme = parameters.theme,
                    background = parameters.background
                };
                Normalize(captureParameters);
                var capture = CaptureScreenshot(target, captureParameters, warnings);
                captures.Add(capture);
            }

            var first = captures[0];
            return new ScreenshotResult
            {
                target = target.info,
                artifacts = captures.SelectMany(capture => capture.artifacts).ToArray(),
                contentWidth = first.contentWidth,
                contentHeight = first.contentHeight,
                tiled = captures.Any(capture => capture.tiled),
                selector = selector,
                captures = captures.ToArray(),
                warnings = warnings.Distinct(StringComparer.Ordinal).ToArray()
            };
        }

        private static ScreenshotCapture CaptureScreenshot(ResolvedTarget target, ScreenshotParameters parameters, List<string> warnings)
        {
            var background = ResolveBackground(parameters.background, parameters.theme);

            using (var session = CreateSession(target, parameters.width, parameters.height, parameters.theme))
            using (var expander = new ScrollViewExpander())
            using (var revealer = new ScrollViewRevealer())
            {
                warnings.AddRange(PreviewStateApplier.Apply(session.Root, target.preview?.state));
                session.ValidateLayout();
                var selector = parameters.selector;
                var selected = ElementInspector.Find(session.Root, selector);
                if (selected == null)
                    throw new InvalidOperationException($"Selector '{selector}' did not match any element.");

                var captureVisibleSelection = !string.IsNullOrEmpty(selector);
                if (captureVisibleSelection)
                {
                    revealer.Reveal(selected, session.ValidateLayout);
                    session.ValidateLayout();
                    captureVisibleSelection = !parameters.fullHeight || IsFullyVisible(selected, session.ViewportBounds);
                }

                var contentHeight = parameters.height;
                var expandedFullHeight = parameters.fullHeight && !captureVisibleSelection;
                if (expandedFullHeight)
                {
                    expander.Expand(session.Root, session.ValidateLayout);
                    contentHeight = ScrollViewExpander.MeasureContentHeight(selected);
                }

                session.ValidateLayout();
                var viewport = session.ViewportBounds;
                var visibleXMin = Mathf.Max(selected.worldBound.xMin, viewport.xMin);
                var visibleXMax = Mathf.Min(selected.worldBound.xMax, viewport.xMin + parameters.width);
                var visibleYMin = expandedFullHeight
                    ? selected.worldBound.yMin
                    : Mathf.Max(selected.worldBound.yMin, viewport.yMin);
                var visibleYMax = expandedFullHeight
                    ? selected.worldBound.yMax
                    : Mathf.Min(selected.worldBound.yMax, viewport.yMin + parameters.height);
                if (!string.IsNullOrEmpty(selector) &&
                    (visibleXMax <= visibleXMin || (!expandedFullHeight && visibleYMax <= visibleYMin)))
                    throw new InvalidOperationException($"Selector '{selector}' is outside the visible capture area.");
                var selectedOriginY = Mathf.Max(0, Mathf.FloorToInt(visibleYMin - viewport.yMin));
                var selectedOriginX = Mathf.Max(0, Mathf.FloorToInt(visibleXMin - viewport.xMin));
                var selectedWidth = Mathf.Max(1, Mathf.CeilToInt(visibleXMax - visibleXMin));
                var selectedHeight = Mathf.Max(1, Mathf.CeilToInt(visibleYMax - visibleYMin));
                var captureWidth = string.IsNullOrEmpty(selector) ? parameters.width : selectedWidth;
                if (captureVisibleSelection)
                    contentHeight = selectedHeight;
                if ((long)captureWidth * contentHeight > MaximumCapturePixels)
                    throw new InvalidOperationException("The requested capture exceeds 100 megapixels. Use a narrower selector or viewport.");

                var tileLimit = Mathf.Min(PreferredTileHeight, SystemInfo.maxTextureSize);
                var artifacts = new List<ScreenshotArtifact>();
                if (captureVisibleSelection)
                {
                    var raw = session.CapturePng(parameters.width, parameters.height, 0, background);
                    var png = PngCropper.Crop(raw, selectedOriginX, selectedOriginY, selectedWidth, selectedHeight);
                    var path = ArtifactStore.WritePng(png);
                    artifacts.Add(new ScreenshotArtifact
                    {
                        path = path,
                        width = selectedWidth,
                        height = selectedHeight,
                        offsetY = 0,
                        viewportWidth = parameters.width
                    });
                }
                else
                {
                    var offset = 0;
                    while (offset < contentHeight)
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
                            offsetY = offset,
                            viewportWidth = parameters.width
                        });
                        offset += tileHeight;
                    }
                }

                warnings.AddRange(session.Warnings);
                if (artifacts.Count > 1)
                    warnings.Add($"The {parameters.width}px capture was split into {artifacts.Count} vertical tiles.");
                return new ScreenshotCapture
                {
                    viewportWidth = parameters.width,
                    viewportHeight = parameters.height,
                    artifacts = artifacts.ToArray(),
                    contentWidth = captureWidth,
                    contentHeight = contentHeight,
                    tiled = artifacts.Count > 1
                };
            }
        }

        private static bool IsFullyVisible(VisualElement element, Rect viewport)
        {
            if (element == null)
                return false;
            var bounds = element.worldBound;
            if (!Contains(viewport, bounds))
                return false;
            var ancestor = element.parent;
            while (ancestor != null)
            {
                if (ancestor is ScrollView scrollView && !Contains(scrollView.contentViewport.worldBound, bounds))
                    return false;
                ancestor = ancestor.parent;
            }
            return true;
        }

        private static bool Contains(Rect container, Rect bounds)
        {
            const float tolerance = 0.5f;
            return bounds.xMin >= container.xMin - tolerance && bounds.yMin >= container.yMin - tolerance &&
                   bounds.xMax <= container.xMax + tolerance && bounds.yMax <= container.yMax + tolerance;
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
            ApplyPreviewDefaults(parameters, preview, null);
        }

        private static void ApplyPreviewDefaults(ScreenshotParameters parameters, PreviewDefinition preview, EditorWindow window)
        {
            if (preview != null)
            {
                if (string.IsNullOrEmpty(parameters.selector))
                    parameters.selector = preview.selector;
                if (!string.IsNullOrEmpty(preview.theme) && (string.IsNullOrEmpty(parameters.theme) || parameters.theme == "editor-dark"))
                    parameters.theme = preview.theme;
                if (!string.IsNullOrEmpty(preview.background) && (string.IsNullOrEmpty(parameters.background) || parameters.background == "theme"))
                    parameters.background = preview.background;
                if (preview.viewport != null)
                {
                    if ((parameters.widths == null || parameters.widths.Length == 0) &&
                        (parameters.width <= 0 || parameters.width == 1280))
                    {
                        if (preview.viewport.widths != null && preview.viewport.widths.Length > 0)
                            parameters.widths = preview.viewport.widths;
                        else if (preview.viewport.width >= 64)
                            parameters.width = preview.viewport.width;
                    }
                    if (string.Equals(preview.viewport.height, "full", StringComparison.OrdinalIgnoreCase) &&
                        (parameters.height <= 0 || parameters.height == 720))
                        parameters.fullHeight = true;
                    else if (int.TryParse(preview.viewport.height, out var configuredHeight) &&
                             (parameters.height <= 0 || parameters.height == 720))
                        parameters.height = configuredHeight;
                }
            }

            ApplyWindowDefaults(window, ref parameters.width, ref parameters.height);
        }

        internal static void ApplyPreviewDefaults(InspectParameters parameters, PreviewDefinition preview, EditorWindow window)
        {
            if (preview != null)
            {
                if (string.IsNullOrEmpty(parameters.selector))
                    parameters.selector = preview.selector;
                if (preview.viewport != null)
                {
                    if ((parameters.width <= 0 || parameters.width == 1280))
                    {
                        var configuredWidth = preview.viewport.widths?.FirstOrDefault(width => width >= 64) ?? 0;
                        if (configuredWidth <= 0)
                            configuredWidth = preview.viewport.width;
                        if (configuredWidth >= 64)
                            parameters.width = configuredWidth;
                    }
                    if (int.TryParse(preview.viewport.height, out var configuredHeight) &&
                        (parameters.height <= 0 || parameters.height == 720))
                        parameters.height = configuredHeight;
                }
            }

            ApplyWindowDefaults(window, ref parameters.width, ref parameters.height);
        }

        private static void ApplyWindowDefaults(EditorWindow window, ref int width, ref int height)
        {
            if (window == null)
                return;
            var bounds = window.rootVisualElement.worldBound;
            if (width <= 0)
                width = Mathf.RoundToInt(bounds.width > 0f ? bounds.width : window.position.width);
            if (height <= 0)
                height = Mathf.RoundToInt(bounds.height > 0f ? bounds.height : window.position.height);
        }

        private static int[] ResolveWidths(ScreenshotParameters parameters)
        {
            var values = parameters.widths != null && parameters.widths.Length > 0
                ? parameters.widths
                : new[] { parameters.width };
            var widths = values
                .Select(width => Mathf.Clamp(width <= 0 ? 1280 : width, 64, SystemInfo.maxTextureSize))
                .Distinct()
                .ToArray();
            return widths.Length == 0 ? new[] { 1280 } : widths;
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
