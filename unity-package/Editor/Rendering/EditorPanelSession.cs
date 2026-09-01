using System;
using System.Collections.Generic;
using System.Reflection;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal sealed class EditorPanelSession : IRenderSession
    {
        private sealed class PanelOwner : ScriptableObject
        {
        }

        private readonly List<string> _warnings = new List<string>();
        private readonly PanelOwner _owner;
        private readonly object _panel;
        private readonly MethodInfo _updateWithoutRepaint;
        private readonly MethodInfo _validateLayout;
        private readonly VisualElement _panelRoot;
        private readonly VisualElement _contentRoot;
        private readonly StyleSheet _themeStyleSheet;
        private int _width;
        private int _height;

        internal static bool IsSupported(out string description)
        {
            var panelType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Panel");
            var editorPanelType = GetEditorPanelType();
            var create = editorPanelType?.GetMethod("FindOrCreate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var repaint = panelType?.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Event) }, null);
            var render = panelType?.GetMethod("Render", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            var update = panelType?.GetMethod("UpdateWithoutRepaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var validate = panelType?.GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var supported = panelType != null && editorPanelType != null && create != null && repaint != null && validate != null;
#if !UNITY_6000_0_OR_NEWER
            supported = supported && update != null;
#endif
            description = supported
                ? $"reflection:{editorPanelType.FullName}.FindOrCreate/Repaint" + (render == null ? string.Empty : "/Render")
                : "unsupported: required Editor Panel members were not found";
            return supported;
        }

        internal EditorPanelSession(VisualTreeAsset document, PreviewDefinition preview, int width, int height)
            : this(CreateDocumentPopulator(document), preview, width, height)
        {
        }

        internal EditorPanelSession(Action<VisualElement> populateDocument, PreviewDefinition preview, int width, int height)
        {
            if (populateDocument == null)
                throw new ArgumentNullException(nameof(populateDocument));

            var panelType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Panel")
                            ?? throw new NotSupportedException("UnityEngine.UIElements.Panel was not found.");
            var editorPanelType = GetEditorPanelType();
            if (editorPanelType == null)
                throw new NotSupportedException("UnityEditor.UIElements.EditorPanel was not found.");
            var create = editorPanelType.GetMethod("FindOrCreate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                         ?? throw new NotSupportedException("EditorPanel.FindOrCreate was not found.");
            _validateLayout = panelType.GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? throw new NotSupportedException("Panel.ValidateLayout was not found.");
#if UNITY_6000_0_OR_NEWER
            _updateWithoutRepaint = null;
#else
            _updateWithoutRepaint = panelType.GetMethod("UpdateWithoutRepaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? throw new NotSupportedException("Panel.UpdateWithoutRepaint was not found.");
#endif
            if (panelType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Event) }, null) == null)
                throw new NotSupportedException("Panel.Repaint(Event) was not found.");

            _owner = ScriptableObject.CreateInstance<PanelOwner>();
            _owner.hideFlags = HideFlags.HideAndDontSave;
            _panel = create.Invoke(null, new object[] { _owner });
            DisableEditorWindowScaling(_panel);
            _panelRoot = ((IPanel)_panel).visualTree;
            _panelRoot.name = "ui-toolkit-mcp-preview-panel";
            _themeStyleSheet = LoadTheme(preview?.theme ?? "editor-dark");
            if (_themeStyleSheet != null)
                _panelRoot.styleSheets.Add(_themeStyleSheet);
            _contentRoot = new VisualElement { name = "ui-toolkit-mcp-preview-document" };
            _panelRoot.Add(_contentRoot);
            ApplyAdditionalStyles(_contentRoot, preview?.stylesheets);
            populateDocument(_contentRoot);
            SetViewport(width, height);
            ValidateLayout();
        }

        private static Action<VisualElement> CreateDocumentPopulator(VisualTreeAsset document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            return document.CloneTree;
        }

        private static Type GetEditorPanelType()
        {
            return Type.GetType("UnityEditor.UIElements.EditorPanel, UnityEditor.UIElementsModule");
        }

        private static void DisableEditorWindowScaling(object panel)
        {
            var basePanelType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.BaseVisualElementPanel");
            var updateScaling = basePanelType?.GetField(
                "UpdateScalingFromEditorWindow",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            updateScaling?.SetValue(panel, false);
        }

        public VisualElement Root => _contentRoot;
        public Rect ViewportBounds => new Rect(_contentRoot.worldBound.xMin, _contentRoot.worldBound.yMin, _width, _height);
        public IReadOnlyList<string> Warnings => _warnings;

        public void SetViewport(int width, int height)
        {
            _width = Mathf.Clamp(width, 64, SystemInfo.maxTextureSize);
            _height = Mathf.Clamp(height, 64, SystemInfo.maxTextureSize);
            _panelRoot.style.width = _width;
            _panelRoot.style.height = _height;
            _contentRoot.style.width = _width;
            _contentRoot.style.minHeight = _height;
            PanelReflection.ConfigureRepaintData(_panel, _width, _height);
        }

        public void ValidateLayout()
        {
            _updateWithoutRepaint?.Invoke(_panel, null);
            _validateLayout.Invoke(_panel, null);
        }

        public byte[] CapturePng(int width, int height, int offsetY, Color background)
        {
            SetViewport(width, height);
            var previousPosition = _contentRoot.transform.position;
            var previousBackground = _panelRoot.style.backgroundColor;
            _contentRoot.transform.position = new Vector3(previousPosition.x, previousPosition.y - offsetY, previousPosition.z);
            _panelRoot.style.backgroundColor = background;
            ValidateLayout();
            // EditorPanel writes UI Toolkit colors as display values. A linear target keeps
            // Linear projects from applying an extra Linear-to-sRGB conversion to those values.
            var renderTexture = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "UI Toolkit MCP Preview"
            };
            try
            {
                return TextureCapture.Capture(renderTexture, () => PanelReflection.PrepareAndRender(_panel), background);
            }
            finally
            {
                _contentRoot.transform.position = previousPosition;
                _panelRoot.style.backgroundColor = previousBackground;
                ValidateLayout();
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private StyleSheet LoadTheme(string theme)
        {
            if (string.Equals(theme, "runtime", StringComparison.OrdinalIgnoreCase))
                return null;
            var dark = !string.Equals(theme, "editor-light", StringComparison.OrdinalIgnoreCase);
            var candidates = dark
                ? new[] { "StyleSheets/DefaultCommonDark.uss", "StyleSheets/Generated/DefaultCommonDark.uss.asset" }
                : new[] { "StyleSheets/DefaultCommonLight.uss", "StyleSheets/Generated/DefaultCommonLight.uss.asset" };
            foreach (var candidate in candidates)
            {
                var styleSheet = EditorGUIUtility.Load(candidate) as StyleSheet;
                if (styleSheet == null)
                    continue;
                var copy = UnityEngine.Object.Instantiate(styleSheet);
                copy.hideFlags = HideFlags.HideAndDontSave;
                var defaultStyleProperty = typeof(StyleSheet).GetProperty(
                    "isDefaultStyleSheet",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                defaultStyleProperty?.SetValue(copy, true, null);
                return copy;
            }
            _warnings.Add($"The built-in {theme} stylesheet was not found; project styles are still applied.");
            return null;
        }

        private void ApplyAdditionalStyles(VisualElement target, string[] paths)
        {
            if (paths == null)
                return;
            foreach (var path in paths)
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet == null)
                    _warnings.Add($"Additional stylesheet was not found: {path}");
                else
                    target.styleSheets.Add(styleSheet);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_panel is IDisposable disposable)
                    disposable.Dispose();
                else
                    _panel.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(_panel, null);
            }
            finally
            {
                if (_themeStyleSheet != null)
                    UnityEngine.Object.DestroyImmediate(_themeStyleSheet);
                UnityEngine.Object.DestroyImmediate(_owner);
            }
        }
    }
}
