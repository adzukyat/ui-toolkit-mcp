using System.Collections.Generic;
using NUnit.Framework;
using UIToolkitMcpPreviewServer.Inspection;
using UIToolkitMcpPreviewServer.Protocol;
using UIToolkitMcpPreviewServer.Targets;
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

        [Test]
        public void AppliesConfiguredValuesVisibilityAndClasses()
        {
            var root = new VisualElement();
            var toggle = new Toggle { name = "choice" };
            var details = new VisualElement { name = "details" };
            root.Add(toggle);
            root.Add(details);

            var state = Json.Deserialize<PreviewDefinition>(
                "{\"state\":{\"#choice\":{\"value\":true,\"addClasses\":[\"selected\"]}," +
                "\"#details\":{\"display\":false,\"enabled\":false}}}").state;
            var warnings = PreviewStateApplier.Apply(root, state);

            Assert.That(warnings, Is.Empty);
            Assert.That(toggle.value, Is.True);
            Assert.That(toggle.ClassListContains("selected"), Is.True);
            Assert.That(details.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(details.enabledSelf, Is.False);
        }

        [Test]
        public void ReportsMissingStateSelectorsWithoutStoppingOtherState()
        {
            var root = new VisualElement();
            var label = new Label { name = "label" };
            root.Add(label);

            var warnings = PreviewStateApplier.Apply(root, new Dictionary<string, PreviewElementState>
            {
                ["#missing"] = new PreviewElementState { display = false },
                ["#label"] = new PreviewElementState { text = "Ready" }
            });

            Assert.That(warnings, Has.Length.EqualTo(1));
            Assert.That(label.text, Is.EqualTo("Ready"));
        }
    }
}
