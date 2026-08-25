using NUnit.Framework;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEngine;

namespace UIToolkitMcpPreviewServer.Tests
{
    internal sealed class ProtocolTests
    {
        [Test]
        public void RequestEnvelopeRoundTrips()
        {
            var source = new RequestEnvelope { id = "42", token = "token", method = "status", payload = "{}" };
            var result = Json.Deserialize<RequestEnvelope>(Json.Serialize(source));
            Assert.That(result.id, Is.EqualTo("42"));
            Assert.That(result.method, Is.EqualTo("status"));
            Assert.That(result.payload, Is.EqualTo("{}"));
        }

        [Test]
        public void DeepElementTreesSerializeWithoutUnityDepthTruncation()
        {
            var root = new ElementInfo { name = "root", children = new ElementInfo[1] };
            var current = root;
            for (var depth = 0; depth < 20; depth++)
            {
                var child = new ElementInfo { name = "level-" + depth, children = new ElementInfo[1] };
                current.children[0] = child;
                current = child;
            }
            current.children = System.Array.Empty<ElementInfo>();

            var serialized = Json.Serialize(root);
            var roundTrip = Json.Deserialize<ElementInfo>(serialized);
            for (var depth = 0; depth < 20; depth++)
                roundTrip = roundTrip.children[0];
            Assert.That(roundTrip.name, Is.EqualTo("level-19"));
        }

        [Test]
        public void DiscoveryDescriptorDoesNotContainEndpointToken()
        {
            var descriptor = new DiscoveryDescriptor
            {
                protocolVersion = ProtocolVersion.Current,
                processId = 123,
                projectPath = "/example/project",
                projectName = "Example Project",
                endpointPath = "/example/project/Library/UIToolkitMcpPreviewServer/endpoint.json",
                unityVersion = "test",
                startedAtUtc = "2026-08-25T00:00:00.000Z"
            };

            var serialized = Json.Serialize(descriptor);
            Assert.That(serialized, Does.Contain("\"schemaVersion\":1"));
            Assert.That(serialized, Does.Not.Contain("token"));
        }

        [TestCase("editor-dark", 56)]
        [TestCase("editor-light", 200)]
        public void ThemeBackgroundUsesUnityEditorCanvasColor(string theme, int channel)
        {
            var color = (Color32)PreviewService.ResolveBackground("theme", theme);
            Assert.That(color, Is.EqualTo(new Color32((byte)channel, (byte)channel, (byte)channel, 255)));
        }

        [Test]
        public void RuntimeThemeBackgroundStaysTransparent()
        {
            Assert.That(PreviewService.ResolveBackground("theme", "runtime"), Is.EqualTo(Color.clear));
        }

        [Test]
        public void ExplicitBackgroundOverridesTheme()
        {
            var color = (Color32)PreviewService.ResolveBackground("#12345678", "editor-dark");
            Assert.That(color, Is.EqualTo(new Color32(0x12, 0x34, 0x56, 0x78)));
        }

        [Test]
        public void PreviewBackgroundAppliesToDefaultScreenshotBackground()
        {
            var parameters = new ScreenshotParameters { background = "theme" };
            PreviewService.ApplyPreviewDefaults(parameters, new PreviewDefinition { background = "#12345678" });
            Assert.That(parameters.background, Is.EqualTo("#12345678"));
        }

        [Test]
        public void ExplicitScreenshotBackgroundOverridesPreviewBackground()
        {
            var parameters = new ScreenshotParameters { background = "#AABBCCDD" };
            PreviewService.ApplyPreviewDefaults(parameters, new PreviewDefinition { background = "#12345678" });
            Assert.That(parameters.background, Is.EqualTo("#AABBCCDD"));
        }
    }
}
