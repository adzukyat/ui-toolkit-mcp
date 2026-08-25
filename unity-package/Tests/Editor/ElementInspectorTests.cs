using NUnit.Framework;
using UIToolkitMcpPreviewServer.Inspection;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Tests
{
    internal sealed class ElementInspectorTests
    {
        [Test]
        public void FindsNameClassAndTypeSelectors()
        {
            var root = new VisualElement();
            var label = new Label("Hello") { name = "greeting" };
            label.AddToClassList("message");
            root.Add(label);

            Assert.That(ElementInspector.Find(root, "#greeting"), Is.SameAs(label));
            Assert.That(ElementInspector.Find(root, ".message"), Is.SameAs(label));
            Assert.That(ElementInspector.Find(root, "Label"), Is.SameAs(label));
        }

        [Test]
        public void DescriptionIncludesStablePublicFields()
        {
            var label = new Label("Hello") { name = "greeting" };
            var result = ElementInspector.Describe(label, 1, false);
            Assert.That(result.path, Is.EqualTo("#greeting"));
            Assert.That(result.text, Is.EqualTo("Hello"));
            Assert.That(result.type, Does.EndWith("Label"));
        }
    }
}

