using NUnit.Framework;
using UIToolkitMcpPreviewServer.Protocol;

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
    }
}
