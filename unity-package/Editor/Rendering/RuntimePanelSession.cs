using System;
using System.Collections.Generic;
using System.Reflection;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal sealed class RuntimePanelSession : IRenderSession
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly GameObject _host;
        private readonly UIDocument _document;
        private readonly PanelSettings _settings;
        private RenderTexture _renderTexture;
        private int _width;
        private int _height;

        internal RuntimePanelSession(VisualTreeAsset document, PreviewDefinition preview, int width, int height)
        {
            var sourceSettings = string.IsNullOrEmpty(preview?.panelSettings)
                ? null
                : AssetDatabase.LoadAssetAtPath<PanelSettings>(preview.panelSettings);
            _settings = sourceSettings == null ? ScriptableObject.CreateInstance<PanelSettings>() : UnityEngine.Object.Instantiate(sourceSettings);
            _settings.hideFlags = HideFlags.HideAndDontSave;
            _settings.scaleMode = PanelScaleMode.ConstantPixelSize;

            _host = new GameObject("UI Toolkit MCP Preview Runtime Host") { hideFlags = HideFlags.HideAndDontSave };
            _document = _host.AddComponent<UIDocument>();
            SetViewport(width, height);
            _document.panelSettings = _settings;
            _document.visualTreeAsset = document;
            ApplyAdditionalStyles(preview?.stylesheets);
            ForceRepaint();
        }

        public VisualElement Root => _document.rootVisualElement;
        public Rect ViewportBounds => new Rect(Root.worldBound.xMin, Root.worldBound.yMin, _width, _height);
        public IReadOnlyList<string> Warnings => _warnings;

        public void SetViewport(int width, int height)
        {
            _width = Mathf.Clamp(width, 64, SystemInfo.maxTextureSize);
            _height = Mathf.Clamp(height, 64, SystemInfo.maxTextureSize);
            if (_renderTexture != null && _renderTexture.width == _width && _renderTexture.height == _height)
                return;
            if (_renderTexture != null)
            {
                _settings.targetTexture = null;
                _renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_renderTexture);
            }
            _renderTexture = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "UI Toolkit Runtime Preview"
            };
            _settings.targetTexture = _renderTexture;
        }

        public void ValidateLayout()
        {
            ForceRepaint();
            var panel = Root?.panel;
            panel?.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(panel, null);
        }

        public byte[] CapturePng(int width, int height, int offsetY, Color background)
        {
            SetViewport(width, height);
            var root = Root;
            var previous = root.transform.position;
            root.transform.position = new Vector3(previous.x, previous.y - offsetY, previous.z);
            try
            {
                return TextureCapture.Capture(_renderTexture, ForceRepaint, background);
            }
            finally
            {
                root.transform.position = previous;
            }
        }

        private void ForceRepaint()
        {
            var panel = Root?.panel;
            if (panel == null)
                return;
            PanelReflection.ConfigureRepaintData(panel, _width, _height);
            PanelReflection.PrepareAndRender(panel);
        }

        private void ApplyAdditionalStyles(string[] paths)
        {
            if (paths == null || Root == null)
                return;
            foreach (var path in paths)
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet == null)
                    _warnings.Add($"Additional stylesheet was not found: {path}");
                else
                    Root.styleSheets.Add(styleSheet);
            }
        }

        public void Dispose()
        {
            if (_settings != null)
                _settings.targetTexture = null;
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(_renderTexture);
            }
            if (_host != null)
                UnityEngine.Object.DestroyImmediate(_host);
            if (_settings != null)
                UnityEngine.Object.DestroyImmediate(_settings);
        }
    }
}
