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
        private readonly ScriptableObject _owner;
        private readonly object _panel;
        private readonly MethodInfo _validateLayout;
        private readonly VisualElement _panelRoot;
        private readonly VisualElement _contentRoot;
        private int _width;
        private int _height;

        internal static bool IsSupported(out string description)
        {
            var panelType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Panel");
            var create = panelType?.GetMethod("CreateEditorPanel", BindingFlags.Static | BindingFlags.NonPublic);
            var repaint = panelType?.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Event) }, null);
            var render = panelType?.GetMethod("Render", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            var validate = panelType?.GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var supported = panelType != null && create != null && repaint != null && validate != null;
            description = supported
                ? $"reflection:{panelType.FullName}.CreateEditorPanel/Repaint" + (render == null ? string.Empty : "/Render")
                : "unsupported: required Editor Panel members were not found";
            return supported;
        }

        internal EditorPanelSession(VisualTreeAsset document, PreviewDefinition preview, int width, int height)
        {
            var panelType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Panel")
                            ?? throw new NotSupportedException("UnityEngine.UIElements.Panel was not found.");
            var create = panelType.GetMethod("CreateEditorPanel", BindingFlags.Static | BindingFlags.NonPublic)
                         ?? throw new NotSupportedException("Panel.CreateEditorPanel was not found.");
            _validateLayout = panelType.GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? throw new NotSupportedException("Panel.ValidateLayout was not found.");
            if (panelType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Event) }, null) == null)
                throw new NotSupportedException("Panel.Repaint(Event) was not found.");

            _owner = ScriptableObject.CreateInstance<PanelOwner>();
            _owner.hideFlags = HideFlags.HideAndDontSave;
            _panel = create.Invoke(null, new object[] { _owner });
            _panelRoot = ((IPanel)_panel).visualTree;
            _panelRoot.name = "ui-toolkit-mcp-preview-panel";
            _contentRoot = document.Instantiate();
            _contentRoot.name = string.IsNullOrEmpty(_contentRoot.name) ? "ui-toolkit-mcp-preview-document" : _contentRoot.name;
            _panelRoot.Add(_contentRoot);
            ApplyTheme(preview?.theme ?? "editor-dark");
            ApplyAdditionalStyles(preview?.stylesheets);
            SetViewport(width, height);
            ValidateLayout();
        }

        public VisualElement Root => _contentRoot;
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
            _validateLayout.Invoke(_panel, null);
        }

        public byte[] CapturePng(int width, int height, int offsetY, Color background)
        {
            SetViewport(width, height);
            var previousPosition = _contentRoot.transform.position;
            _contentRoot.transform.position = new Vector3(previousPosition.x, previousPosition.y - offsetY, previousPosition.z);
            ValidateLayout();
            var renderTexture = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
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
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private void ApplyTheme(string theme)
        {
            if (string.Equals(theme, "runtime", StringComparison.OrdinalIgnoreCase))
                return;
            var dark = !string.Equals(theme, "editor-light", StringComparison.OrdinalIgnoreCase);
            var candidates = dark
                ? new[] { "StyleSheets/DefaultCommonDark.uss", "StyleSheets/Generated/DefaultCommonDark.uss.asset" }
                : new[] { "StyleSheets/DefaultCommonLight.uss", "StyleSheets/Generated/DefaultCommonLight.uss.asset" };
            foreach (var candidate in candidates)
            {
                var styleSheet = EditorGUIUtility.Load(candidate) as StyleSheet;
                if (styleSheet == null)
                    continue;
                _panelRoot.styleSheets.Add(styleSheet);
                return;
            }
            _warnings.Add($"The built-in {theme} stylesheet was not found; project styles are still applied.");
        }

        private void ApplyAdditionalStyles(string[] paths)
        {
            if (paths == null)
                return;
            foreach (var path in paths)
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet == null)
                    _warnings.Add($"Additional stylesheet was not found: {path}");
                else
                    _contentRoot.styleSheets.Add(styleSheet);
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
                UnityEngine.Object.DestroyImmediate(_owner);
            }
        }
    }
}
