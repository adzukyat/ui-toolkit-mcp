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
        private readonly Vector3 _position;

        internal EditorWindowSession(EditorWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _panel = window.rootVisualElement.panel ?? throw new InvalidOperationException("The EditorWindow is not attached to a panel.");
            _validateLayout = _panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? throw new NotSupportedException("The EditorWindow panel has no ValidateLayout method.");
            _position = Root.transform.position;
        }

        public VisualElement Root => _window.rootVisualElement;
        public IReadOnlyList<string> Warnings => Array.Empty<string>();

        public void SetViewport(int width, int height)
        {
            // A live EditorWindow shares its panel with Unity's dock chrome. Changing the
            // root size reflows that shared hierarchy and can place the dock tabs over the
            // window content. The requested size is only the capture canvas for windows.
        }

        public void ValidateLayout()
        {
            _validateLayout.Invoke(_panel, null);
        }

        public byte[] CapturePng(int width, int height, int offsetY, Color background)
        {
            ValidateLayout();
            var rootBounds = Root.worldBound;
            var worldClip = PanelReflection.GetWorldClip(Root);
            var insetX = Mathf.Max(0, Mathf.CeilToInt(worldClip.xMin - rootBounds.xMin));
            var insetY = Mathf.Max(0, Mathf.CeilToInt(worldClip.yMin - rootBounds.yMin));
            var originX = Mathf.Max(0, Mathf.FloorToInt(rootBounds.xMin) + insetX);
            var originY = Mathf.Max(0, Mathf.FloorToInt(rootBounds.yMin) + insetY);
            if (width > SystemInfo.maxTextureSize - originX || height > SystemInfo.maxTextureSize - originY)
                throw new InvalidOperationException("The EditorWindow capture plus its panel offset exceeds the maximum texture size.");
            var previous = Root.transform.position;
            Root.transform.position = new Vector3(previous.x + insetX, previous.y + insetY - offsetY, previous.z);
            ValidateLayout();
            var renderWidth = width + originX;
            var renderHeight = height + originY;
            var renderTexture = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "UI Toolkit EditorWindow Preview"
            };
            try
            {
                using (PanelReflection.OverrideRepaintData(_panel, renderWidth, renderHeight))
                {
                    var panelPng = TextureCapture.Capture(renderTexture, () => PanelReflection.PrepareAndRender(_panel), background);
                    return PngCropper.Crop(panelPng, originX, originY, width, height);
                }
            }
            finally
            {
                Root.transform.position = previous;
                ValidateLayout();
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        public void Dispose()
        {
            Root.transform.position = _position;
            _window.Repaint();
        }
    }
}
