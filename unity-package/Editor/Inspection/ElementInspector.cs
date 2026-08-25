using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Inspection
{
    internal static class ElementInspector
    {
        internal static VisualElement Find(VisualElement root, string selector)
        {
            if (root == null)
                return null;
            if (string.IsNullOrWhiteSpace(selector) || selector == ":root")
                return root;

            selector = selector.Trim();
            if (selector[0] == '#')
                return root.Q(selector.Substring(1));
            if (selector[0] == '.')
                return root.Query<VisualElement>(className: selector.Substring(1)).First();

            foreach (var element in root.Query<VisualElement>().ToList())
            {
                var type = element.GetType();
                if (string.Equals(element.name, selector, StringComparison.Ordinal) ||
                    string.Equals(type.Name, selector, StringComparison.Ordinal) ||
                    string.Equals(type.FullName, selector, StringComparison.Ordinal))
                    return element;
            }
            return null;
        }

        internal static ElementInfo Describe(VisualElement element, int depth, bool includeStyles)
        {
            if (element == null)
                return null;
            depth = Mathf.Clamp(depth, 0, 64);
            return DescribeRecursive(element, depth, includeStyles, BuildPath(element));
        }

        private static ElementInfo DescribeRecursive(VisualElement element, int remainingDepth, bool includeStyles, string path)
        {
            var children = new List<ElementInfo>();
            if (remainingDepth > 0)
            {
                for (var index = 0; index < element.hierarchy.childCount; index++)
                {
                    var child = element.hierarchy[index];
                    children.Add(DescribeRecursive(child, remainingDepth - 1, includeStyles, BuildPath(child)));
                }
            }

            return new ElementInfo
            {
                path = path,
                type = element.GetType().FullName,
                name = element.name,
                classes = element.GetClasses().OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                text = ReadText(element),
                value = ReadValue(element),
                visible = element.visible && element.resolvedStyle.display != DisplayStyle.None && element.resolvedStyle.visibility == Visibility.Visible,
                enabled = element.enabledInHierarchy,
                focusable = element.focusable,
                pickingMode = element.pickingMode.ToString(),
                layout = Rect(element.layout),
                worldBound = Rect(element.worldBound),
                contentRect = Rect(element.contentRect),
                resolvedStyle = includeStyles ? ResolvedStyles(element) : Array.Empty<StylePropertyInfo>(),
                children = children.ToArray()
            };
        }

        private static string BuildPath(VisualElement element)
        {
            if (!string.IsNullOrEmpty(element.name))
                return "#" + element.name;
            var segments = new Stack<string>();
            var current = element;
            while (current != null)
            {
                var index = current.parent == null ? 0 : current.parent.IndexOf(current);
                segments.Push(current.GetType().Name + "[" + index + "]");
                current = current.parent;
            }
            return string.Join("/", segments);
        }

        private static string ReadText(VisualElement element)
        {
            return element is TextElement textElement ? textElement.text : null;
        }

        private static string ReadValue(VisualElement element)
        {
            try
            {
                var property = element.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public);
                if (property == null || property.GetIndexParameters().Length != 0)
                    return null;
                var value = property.GetValue(element, null);
                if (value == null || value is UnityEngine.Object)
                    return value == null ? null : ((UnityEngine.Object)value).name;
                if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum || value is decimal)
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                // A custom control is allowed to throw from a getter; inspection continues.
            }
            return null;
        }

        private static StylePropertyInfo[] ResolvedStyles(VisualElement element)
        {
            var style = element.resolvedStyle;
            return new[]
            {
                Property("display", style.display), Property("visibility", style.visibility), Property("position", style.position),
                Property("left", style.left), Property("top", style.top), Property("right", style.right), Property("bottom", style.bottom),
                Property("width", style.width), Property("height", style.height), Property("minWidth", style.minWidth), Property("minHeight", style.minHeight),
                Property("maxWidth", style.maxWidth), Property("maxHeight", style.maxHeight),
                Property("flexDirection", style.flexDirection), Property("flexGrow", style.flexGrow), Property("flexShrink", style.flexShrink),
                Property("flexBasis", style.flexBasis), Property("justifyContent", style.justifyContent), Property("alignItems", style.alignItems),
                Property("marginLeft", style.marginLeft), Property("marginTop", style.marginTop), Property("marginRight", style.marginRight), Property("marginBottom", style.marginBottom),
                Property("paddingLeft", style.paddingLeft), Property("paddingTop", style.paddingTop), Property("paddingRight", style.paddingRight), Property("paddingBottom", style.paddingBottom),
                Property("borderLeftWidth", style.borderLeftWidth), Property("borderTopWidth", style.borderTopWidth), Property("borderRightWidth", style.borderRightWidth), Property("borderBottomWidth", style.borderBottomWidth),
                Property("color", style.color), Property("backgroundColor", style.backgroundColor), Property("opacity", style.opacity), Property("overflow", element.style.overflow),
                Property("fontSize", style.fontSize), Property("unityTextAlign", style.unityTextAlign), Property("whiteSpace", style.whiteSpace)
            };
        }

        private static StylePropertyInfo Property(string name, object value)
        {
            string serialized;
            if (value is float number)
                serialized = number.ToString("0.###", CultureInfo.InvariantCulture);
            else if (value is Color color)
                serialized = "#" + ColorUtility.ToHtmlStringRGBA(color);
            else
                serialized = Convert.ToString(value, CultureInfo.InvariantCulture);
            return new StylePropertyInfo { name = name, value = serialized };
        }

        private static RectInfo Rect(Rect value)
        {
            return new RectInfo { x = value.x, y = value.y, width = value.width, height = value.height };
        }
    }
}
