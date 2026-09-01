using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UIToolkitMcpPreviewServer.Inspection;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Targets
{
    internal static class PreviewStateApplier
    {
        internal static string[] Apply(VisualElement root, IDictionary<string, PreviewElementState> states)
        {
            if (root == null || states == null || states.Count == 0)
                return Array.Empty<string>();

            var warnings = new List<string>();
            foreach (var entry in states)
            {
                var element = ElementInspector.Find(root, entry.Key);
                if (element == null)
                {
                    warnings.Add($"Preview state selector '{entry.Key}' did not match any element.");
                    continue;
                }

                try
                {
                    Apply(element, entry.Value ?? new PreviewElementState());
                }
                catch (Exception exception)
                {
                    warnings.Add($"Could not apply preview state for '{entry.Key}': {exception.Message}");
                }
            }
            return warnings.ToArray();
        }

        private static void Apply(VisualElement element, PreviewElementState state)
        {
            if (state.value != null)
                SetProperty(element, "value", state.value);
            if (state.selectedIndex.HasValue)
                SetProperty(element, "selectedIndex", JToken.FromObject(state.selectedIndex.Value));
            if (state.text != null)
            {
                if (!(element is TextElement textElement))
                    throw new InvalidOperationException($"{element.GetType().Name} has no text value.");
                textElement.text = state.text;
            }
            if (state.display.HasValue)
                element.style.display = state.display.Value ? DisplayStyle.Flex : DisplayStyle.None;
            if (state.visible.HasValue)
                element.visible = state.visible.Value;
            if (state.enabled.HasValue)
                element.SetEnabled(state.enabled.Value);

            foreach (var className in state.removeClasses ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(className))
                    element.RemoveFromClassList(className);
            }
            foreach (var className in state.addClasses ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(className))
                    element.AddToClassList(className);
            }
        }

        private static void SetProperty(VisualElement element, string name, JToken value)
        {
            var property = element.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
            {
                property.SetValue(element, Convert(value, property.PropertyType), null);
                return;
            }

            var setter = element.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method => method.Name == "SetValueWithoutNotify" && method.GetParameters().Length == 1);
            if (name == "value" && setter != null)
            {
                var parameterType = setter.GetParameters()[0].ParameterType;
                setter.Invoke(element, new[] { Convert(value, parameterType) });
                return;
            }

            throw new InvalidOperationException($"{element.GetType().Name} has no writable {name}.");
        }

        private static object Convert(JToken value, Type targetType)
        {
            try
            {
                return value.ToObject(targetType);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Value {value.ToString(Newtonsoft.Json.Formatting.None)} is invalid for {targetType.Name}.", exception);
            }
        }
    }
}
