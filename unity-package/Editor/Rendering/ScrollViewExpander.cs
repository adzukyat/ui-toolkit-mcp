using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal sealed class ScrollViewExpander : IDisposable
    {
        private sealed class Snapshot
        {
            internal ScrollView scrollView;
            internal StyleLength height;
            internal StyleLength maxHeight;
            internal StyleEnum<Overflow> overflow;
            internal ScrollerVisibility verticalVisibility;
            internal Vector2 offset;
        }

        private readonly List<Snapshot> _snapshots = new List<Snapshot>();

        internal void Expand(VisualElement root, Action validateLayout)
        {
            foreach (var scrollView in root.Query<ScrollView>().ToList())
            {
                _snapshots.Add(new Snapshot
                {
                    scrollView = scrollView,
                    height = scrollView.style.height,
                    maxHeight = scrollView.style.maxHeight,
                    overflow = scrollView.style.overflow,
                    verticalVisibility = scrollView.verticalScrollerVisibility,
                    offset = scrollView.scrollOffset
                });
            }

            for (var pass = 0; pass < 3; pass++)
            {
                validateLayout();
                foreach (var snapshot in _snapshots)
                {
                    var scrollView = snapshot.scrollView;
                    var contentHeight = Mathf.Max(scrollView.contentContainer.layout.height, MaximumBottom(scrollView.contentContainer));
                    scrollView.scrollOffset = Vector2.zero;
                    scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                    scrollView.style.maxHeight = StyleKeyword.None;
                    scrollView.style.height = Mathf.Ceil(contentHeight + scrollView.resolvedStyle.paddingTop + scrollView.resolvedStyle.paddingBottom + 2f);
                    scrollView.style.overflow = Overflow.Visible;
                }
            }
            validateLayout();
        }

        internal static int MeasureContentHeight(VisualElement root)
        {
            if (root == null)
                return 64;
            var origin = root.worldBound.yMin;
            var bottom = MaximumWorldBottom(root);
            return Mathf.Max(64, Mathf.CeilToInt(bottom - origin));
        }

        private static float MaximumBottom(VisualElement root)
        {
            var maximum = root.layout.yMax;
            for (var index = 0; index < root.hierarchy.childCount; index++)
            {
                var child = root.hierarchy[index];
                if (child.resolvedStyle.display == DisplayStyle.None)
                    continue;
                maximum = Mathf.Max(maximum, child.layout.y + MaximumBottom(child));
            }
            return maximum;
        }

        private static float MaximumWorldBottom(VisualElement root)
        {
            var maximum = root.worldBound.yMax;
            for (var index = 0; index < root.hierarchy.childCount; index++)
            {
                var child = root.hierarchy[index];
                if (child.resolvedStyle.display == DisplayStyle.None)
                    continue;
                maximum = Mathf.Max(maximum, MaximumWorldBottom(child));
            }
            return maximum;
        }

        public void Dispose()
        {
            foreach (var snapshot in _snapshots)
            {
                if (snapshot.scrollView == null)
                    continue;
                snapshot.scrollView.style.height = snapshot.height;
                snapshot.scrollView.style.maxHeight = snapshot.maxHeight;
                snapshot.scrollView.style.overflow = snapshot.overflow;
                snapshot.scrollView.verticalScrollerVisibility = snapshot.verticalVisibility;
                snapshot.scrollView.scrollOffset = snapshot.offset;
            }
            _snapshots.Clear();
        }
    }
}
