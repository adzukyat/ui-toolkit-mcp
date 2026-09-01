#if UNITY_INCLUDE_TESTS
using UnityEditor;
using UnityEngine.UIElements;

namespace McpPreviewFixtures
{
    public sealed class PreviewStyleFixtureElement : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<PreviewStyleFixtureElement>
        {
            public override VisualElement Create(IUxmlAttributes bag, CreationContext cc)
            {
                var element = (PreviewStyleFixtureElement)base.Create(bag, cc);
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/me.adzuki.ui-toolkit-mcp.preview-server/Tests/Editor/Fixtures/CustomControlFixture.uss");
                if (styleSheet != null)
                    element.styleSheets.Add(styleSheet);
                element.style.width = 300;
                element.style.flexDirection = FlexDirection.Row;
                element.Add(new Button { name = "first-button", text = "First" });
                element.Add(new Button { name = "second-button", text = "Second" });
                return element;
            }
        }
    }
}
#endif
