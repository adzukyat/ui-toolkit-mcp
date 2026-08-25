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
    }
}
