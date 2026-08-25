using System;
using System.Reflection;
using UnityEngine;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal static class PanelReflection
    {
        internal static void ConfigureRepaintData(object panel, int width, int height)
        {
            if (panel == null)
                return;
            var repaintData = panel.GetType()
                .GetProperty("repaintData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(panel, null);
            if (repaintData == null)
                return;
            Set(repaintData, "currentOffset", Matrix4x4.identity);
            Set(repaintData, "mousePosition", Vector2.zero);
            Set(repaintData, "currentWorldClip", new Rect(0, 0, width, height));
        }

        internal static void PrepareAndRender(object panel)
        {
            if (panel == null)
                throw new InvalidOperationException("The UI Toolkit panel is not available.");
            var type = panel.GetType();
            var repaint = type.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Event) }, null)
                          ?? throw new NotSupportedException("Panel.Repaint(Event) was not found.");
            var render = type.GetMethod("Render", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            var repaintEvent = new Event { type = EventType.Repaint };
            repaint.Invoke(panel, new object[] { repaintEvent });
            render?.Invoke(panel, null);
        }

        private static void Set(object target, string propertyName, object value)
        {
            target.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(target, value, null);
        }
    }
}
