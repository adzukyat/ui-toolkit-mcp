using System.IO;
using NUnit.Framework;
using UIToolkitMcpPreviewServer.Protocol;
using UIToolkitMcpPreviewServer.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Tests
{
    internal sealed class EditorPanelSessionTests
    {
        private sealed class WindowFixture : EditorWindow
        {
        }

        private const string RuntimeFixturePath = "Packages/me.adzuki.ui-toolkit-mcp.preview-server/Tests/Editor/Fixtures/RuntimeFixture.uxml";
        private const string EditorFixturePath = "Packages/me.adzuki.ui-toolkit-mcp.preview-server/Tests/Editor/Fixtures/EditorFixture.uxml";

        [TestCase(RuntimeFixturePath)]
        [TestCase(EditorFixturePath)]
        public void RendersDocumentToPng(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null);
            Assert.That(EditorPanelSession.IsSupported(out var description), Is.True, description);

            using (var session = new EditorPanelSession(asset, new PreviewDefinition { theme = "editor-dark" }, 400, 300))
            {
                var png = session.CapturePng(400, 300, 0, new Color(0f, 0f, 0f, 0f));
                Assert.That(png, Has.Length.GreaterThan(100));
                Assert.That(png[0], Is.EqualTo(0x89));
                Assert.That(session.Root.Q<Label>(), Is.Not.Null);
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        Assert.That(texture.LoadImage(png), Is.True);
                        var pixels = texture.GetPixels32();
                        Assert.That(pixels, Has.Some.Not.EqualTo(pixels[0]), "Rendered PNG should contain UI pixels.");
                    }
                    finally
                    {
                        Object.DestroyImmediate(texture);
                    }
                }
            }
        }

        [Test]
        public void ExpandingScrollViewRestoresInlineState()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RuntimeFixturePath);
            using (var session = new EditorPanelSession(asset, new PreviewDefinition { theme = "editor-dark" }, 400, 240))
            {
                var scroll = session.Root.Q<ScrollView>("scroll");
                var originalHeight = scroll.style.height;
                var originalOffset = scroll.scrollOffset;
                using (var expander = new ScrollViewExpander())
                    expander.Expand(session.Root, session.ValidateLayout);
                Assert.That(scroll.style.height.value, Is.EqualTo(originalHeight.value));
                Assert.That(scroll.scrollOffset, Is.EqualTo(originalOffset));
            }
        }

        [Test]
        public void ScreenshotSelectorCropsToElementBounds()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required for screenshot verification.");

            var result = PreviewService.Screenshot(new ScreenshotParameters
            {
                target = new TargetReference { id = RuntimeFixturePath },
                selector = "#title",
                width = 400,
                height = 300,
                theme = "editor-dark",
                background = "#00000000"
            });
            Assert.That(result.artifacts, Has.Length.EqualTo(1));
            Assert.That(result.artifacts[0].width, Is.EqualTo(296));
            Assert.That(result.artifacts[0].height, Is.EqualTo(40));
            Assert.That(File.Exists(result.artifacts[0].path), Is.True);
            File.Delete(result.artifacts[0].path);
        }

        [TestCase("theme", 255)]
        [TestCase("#00000000", 0)]
        public void EditorCanvasHonorsBackgroundMode(string background, int expectedAlpha)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required for screenshot verification.");

            var result = PreviewService.Screenshot(new ScreenshotParameters
            {
                target = new TargetReference { id = EditorFixturePath },
                width = 400,
                height = 300,
                theme = "editor-dark",
                background = background
            });
            var path = result.artifacts[0].path;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True);
                var corner = (Color32)texture.GetPixel(texture.width - 1, texture.height - 1);
                Assert.That(corner.a, Is.EqualTo(expectedAlpha));
                if (expectedAlpha > 0)
                {
                    Assert.That(corner.r, Is.GreaterThan(0), "The themed canvas must not remain transparent black.");
                    Assert.That(corner.r, Is.EqualTo(corner.g));
                    Assert.That(corner.g, Is.EqualTo(corner.b));
                }
            }
            finally
            {
                Object.DestroyImmediate(texture);
                File.Delete(path);
            }
        }

        [Test]
        public void EditorWindowCaptureStartsAtContentWithoutResizingLiveRoot()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to attach an EditorWindow.");

            var window = ScriptableObject.CreateInstance<WindowFixture>();
            try
            {
                window.rootVisualElement.style.backgroundColor = Color.magenta;
                var header = new VisualElement();
                header.style.height = 40;
                header.style.flexShrink = 0;
                header.style.backgroundColor = Color.green;
                window.rootVisualElement.Add(header);
                window.position = new Rect(100, 100, 320, 180);
                window.Show();
                if (window.rootVisualElement.panel == null)
                    Assert.Ignore("The test EditorWindow was not attached to a panel.");

                var originalWidth = window.rootVisualElement.style.width;
                var originalHeight = window.rootVisualElement.style.height;
                using (var session = new EditorWindowSession(window))
                {
                    session.SetViewport(1600, 1200);
                    var png = session.CapturePng(320, 180, 0, Color.clear);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        Assert.That(texture.LoadImage(png), Is.True);
                        Assert.That(texture.width, Is.EqualTo(320));
                        Assert.That(texture.height, Is.EqualTo(180));
                        var sampleX = Mathf.Min(texture.width / 2, Mathf.Max(1, Mathf.FloorToInt(window.rootVisualElement.worldBound.width / 2)));
                        var topCenter = texture.GetPixel(sampleX, 10);
                        var bottomCenter = texture.GetPixel(sampleX, texture.height - 10);
                        Assert.That(topCenter.r, Is.GreaterThan(0.7f),
                            $"The window content should start at the top of the capture. top={topCenter}, bottom={bottomCenter}, root={window.rootVisualElement.worldBound}, header={header.worldBound}");
                        Assert.That(topCenter.b, Is.GreaterThan(0.7f), "Editor chrome should not cover the window content.");
                        Assert.That(topCenter.g, Is.LessThan(0.3f), "Editor chrome should not cover the window content.");
                    }
                    finally
                    {
                        Object.DestroyImmediate(texture);
                    }
                }

                Assert.That(window.rootVisualElement.style.width.value, Is.EqualTo(originalWidth.value));
                Assert.That(window.rootVisualElement.style.height.value, Is.EqualTo(originalHeight.value));
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(window);
            }
        }
    }
}
