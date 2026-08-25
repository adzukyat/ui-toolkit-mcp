using System;
using UnityEngine;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal static class PngCropper
    {
        internal static byte[] Crop(byte[] png, int x, int yFromTop, int width, int height)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Texture2D cropped = null;
            try
            {
                if (!source.LoadImage(png, false))
                    throw new InvalidOperationException("Unity could not decode the captured PNG.");
                x = Mathf.Clamp(x, 0, source.width - 1);
                yFromTop = Mathf.Clamp(yFromTop, 0, source.height - 1);
                width = Mathf.Clamp(width, 1, source.width - x);
                height = Mathf.Clamp(height, 1, source.height - yFromTop);
                var sourceY = source.height - yFromTop - height;
                cropped = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                cropped.SetPixels(source.GetPixels(x, sourceY, width, height));
                cropped.Apply(false, false);
                return cropped.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (cropped != null)
                    UnityEngine.Object.DestroyImmediate(cropped);
            }
        }
    }
}
