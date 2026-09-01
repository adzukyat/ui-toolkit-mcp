using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal interface IRenderSession : IDisposable
    {
        VisualElement Root { get; }
        Rect ViewportBounds { get; }
        IReadOnlyList<string> Warnings { get; }
        void SetViewport(int width, int height);
        void ValidateLayout();
        byte[] CapturePng(int width, int height, int offsetY, Color background);
    }
}
