using System;
using UnityEngine;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal static class TextureCapture
    {
        internal static byte[] Capture(RenderTexture renderTexture, Action repaint, Color background)
        {
            var previous = RenderTexture.active;
            var previousCamera = Camera.current;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false, false);
            try
            {
                if (!renderTexture.IsCreated())
                    renderTexture.Create();
                Camera.SetupCurrent(null);
                RenderTexture.active = renderTexture;
                GL.Viewport(new Rect(0, 0, renderTexture.width, renderTexture.height));
                GL.Clear(true, true, background);
                repaint();

                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                Camera.SetupCurrent(previousCamera);
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

    }
}
