using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal sealed class EditorWindowSession : IRenderSession
    {
        private readonly EditorWindow _window;
        private readonly object _panel;
        private readonly MethodInfo _validateLayout;
        private readonly StyleLength _width;
        private readonly StyleLength _height;
        private readonly Vector3 _position;

        internal EditorWindowSession(EditorWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _panel = window.rootVisualElement.panel ?? throw new InvalidOperationException("The EditorWindow is not attached to a panel.");
            _validateLayout = _panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? throw new NotSupportedException("The EditorWindow panel has no ValidateLayout method.");
            _width = Root.style.width;
            _height = Root.style.height;
            _position = Root.transform.position;
        }

        public VisualElement Root => _window.rootVisualElement;
        public IReadOnlyList<string> Warnings => Array.Empty<string>();

        public void SetViewport(int width, int height)
        {
            Root.style.width = Mathf.Max(64, width);
            Root.style.height = Mathf.Max(64, height);
            PanelReflection.ConfigureRepaintData(_panel, width, height);
        }

        public void ValidateLayout()
        {
            _validateLayout.Invoke(_panel, null);
        }

        public byte[] CapturePng(int width, int height, int offsetY, Color background)
        {
            SetViewport(width, height);
            var previous = Root.transform.position;
            Root.transform.position = new Vector3(previous.x, previous.y - offsetY, previous.z);
            ValidateLayout();
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "UI Toolkit EditorWindow Preview"
            };
            try
            {
                return TextureCapture.Capture(renderTexture, () => PanelReflection.PrepareAndRender(_panel), background);
            }
            finally
            {
                Root.transform.position = previous;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        public void Dispose()
        {
            Root.transform.position = _position;
            Root.style.width = _width;
            Root.style.height = _height;
            _window.Repaint();
        }
    }
}
