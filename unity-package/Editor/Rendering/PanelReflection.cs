using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal static class PanelReflection
    {
        internal static void ConfigureRepaintData(object panel, int width, int height)
        {
            var repaintData = GetRepaintData(panel);
            if (repaintData != null)
                ApplyRepaintData(repaintData, width, height);
        }

        internal static IDisposable OverrideRepaintData(object panel, int width, int height)
        {
            var repaintData = GetRepaintData(panel);
            if (repaintData == null)
                return EmptyScope.Instance;
            var snapshot = new RepaintDataScope(repaintData);
            ApplyRepaintData(repaintData, width, height);
            return snapshot;
        }

        internal static Rect GetWorldClip(VisualElement element)
        {
            if (element == null)
                return Rect.zero;
            var property = typeof(VisualElement).GetProperty("worldClip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(element, null) is Rect clip ? clip : element.worldBound;
        }

        private static object GetRepaintData(object panel)
        {
            if (panel == null)
                return null;
            return panel.GetType()
                .GetProperty("repaintData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(panel, null);
        }

        private static void ApplyRepaintData(object repaintData, int width, int height)
        {
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

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new EmptyScope();
            public void Dispose()
            {
            }
        }

        private sealed class RepaintDataScope : IDisposable
        {
            private readonly object _target;
            private readonly object _currentOffset;
            private readonly object _mousePosition;
            private readonly object _currentWorldClip;

            internal RepaintDataScope(object target)
            {
                _target = target;
                _currentOffset = Get(target, "currentOffset");
                _mousePosition = Get(target, "mousePosition");
                _currentWorldClip = Get(target, "currentWorldClip");
            }

            public void Dispose()
            {
                Set(_target, "currentOffset", _currentOffset);
                Set(_target, "mousePosition", _mousePosition);
                Set(_target, "currentWorldClip", _currentWorldClip);
            }

            private static object Get(object target, string propertyName)
            {
                return target.GetType()
                    .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(target, null);
            }
        }
    }
}
