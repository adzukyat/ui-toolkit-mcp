using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal sealed class ScrollViewRevealer : IDisposable
    {
        private readonly List<KeyValuePair<ScrollView, Vector2>> _offsets = new List<KeyValuePair<ScrollView, Vector2>>();

        internal void Reveal(VisualElement element, Action validateLayout)
        {
            var current = element?.parent;
            while (current != null)
            {
                if (current is ScrollView scrollView)
                    _offsets.Add(new KeyValuePair<ScrollView, Vector2>(scrollView, scrollView.scrollOffset));
                current = current.parent;
            }

            for (var index = _offsets.Count - 1; index >= 0; index--)
            {
                _offsets[index].Key.ScrollTo(element);
                validateLayout();
            }
        }

        public void Dispose()
        {
            foreach (var entry in _offsets)
            {
                if (entry.Key != null)
                    entry.Key.scrollOffset = entry.Value;
            }
            _offsets.Clear();
        }
    }
}
