using System;

namespace UIToolkitMcpPreviewServer.Protocol
{
    internal static class ProtocolVersion
    {
        internal const int Current = 1;
    }

    [Serializable]
    internal sealed class RequestEnvelope
    {
        public string id;
        public string token;
        public string method;
        public string payload;
    }

    [Serializable]
    internal sealed class ResponseEnvelope
    {
        public string id;
        public bool ok;
        public string payload;
        public ProtocolError error;

        internal static ResponseEnvelope Success(string id, object value)
        {
            return new ResponseEnvelope
            {
                id = id,
                ok = true,
                payload = Json.Serialize(value)
            };
        }

        internal static ResponseEnvelope Failure(string id, string code, string message, string details = null)
        {
            return new ResponseEnvelope
            {
                id = id,
                ok = false,
                error = new ProtocolError { code = code, message = message, details = details }
            };
        }
    }

    [Serializable]
    internal sealed class ProtocolError
    {
        public string code;
        public string message;
        public string details;
    }

    [Serializable]
    internal sealed class EndpointDescriptor
    {
        public int protocolVersion;
        public int port;
        public int processId;
        public string projectPath;
        public string unityVersion;
        public string token;
        public string startedAtUtc;
    }

    [Serializable]
    internal sealed class DiscoveryDescriptor
    {
        public int schemaVersion = 1;
        public int protocolVersion;
        public int processId;
        public string projectPath;
        public string projectName;
        public string endpointPath;
        public string unityVersion;
        public string startedAtUtc;
    }

    [Serializable]
    internal sealed class EmptyParameters
    {
    }

    [Serializable]
    internal sealed class ListTargetsParameters
    {
        public string query;
        public bool includePackages = true;
    }

    [Serializable]
    internal sealed class TargetReference
    {
        public string kind;
        public string id;
    }

    [Serializable]
    internal sealed class InspectParameters
    {
        public TargetReference target;
        public string selector;
        public int depth = 8;
        public bool includeResolvedStyles = true;
        public int width = 1280;
        public int height = 720;
    }

    [Serializable]
    internal sealed class ScreenshotParameters
    {
        public TargetReference target;
        public string selector;
        public int width = 1280;
        public int height = 720;
        public bool fullHeight;
        public string theme = "editor-dark";
        public string background = "#00000000";
    }

    [Serializable]
    internal sealed class ReloadParameters
    {
        public string[] paths;
    }

    [Serializable]
    internal sealed class StatusResult
    {
        public int protocolVersion;
        public string projectPath;
        public string unityVersion;
        public int processId;
        public bool isCompiling;
        public bool isUpdating;
        public string[] capabilities;
        public string editorRenderer;
        public string[] warnings;
    }

    [Serializable]
    internal sealed class TargetInfo
    {
        public string kind;
        public string id;
        public string name;
        public string path;
        public string type;
        public string title;
        public bool editorOnly;
        public bool configured;
    }

    [Serializable]
    internal sealed class ListTargetsResult
    {
        public TargetInfo[] targets;
        public string[] warnings;
    }

    [Serializable]
    internal sealed class RectInfo
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    internal sealed class StylePropertyInfo
    {
        public string name;
        public string value;
    }

    [Serializable]
    internal sealed class ElementInfo
    {
        public string path;
        public string type;
        public string name;
        public string[] classes;
        public string text;
        public string value;
        public bool visible;
        public bool enabled;
        public bool focusable;
        public string pickingMode;
        public RectInfo layout;
        public RectInfo worldBound;
        public RectInfo contentRect;
        public StylePropertyInfo[] resolvedStyle;
        public ElementInfo[] children;
    }

    [Serializable]
    internal sealed class InspectResult
    {
        public int schemaVersion = 1;
        public TargetInfo target;
        public int viewportWidth;
        public int viewportHeight;
        public string selector;
        public ElementInfo root;
        public string[] warnings;
    }

    [Serializable]
    internal sealed class ScreenshotArtifact
    {
        public string path;
        public int width;
        public int height;
        public int offsetY;
        public string mimeType = "image/png";
    }

    [Serializable]
    internal sealed class ScreenshotResult
    {
        public int schemaVersion = 1;
        public TargetInfo target;
        public ScreenshotArtifact[] artifacts;
        public int contentWidth;
        public int contentHeight;
        public bool tiled;
        public string selector;
        public string[] warnings;
    }

    [Serializable]
    internal sealed class ReloadResult
    {
        public string[] importedPaths;
        public bool refreshedAll;
    }

    [Serializable]
    internal sealed class PreviewConfiguration
    {
        public int schemaVersion;
        public PreviewDefinition[] previews;
    }

    [Serializable]
    internal sealed class PreviewDefinition
    {
        public string alias;
        public string document;
        public string[] stylesheets;
        public string panelSettings;
        public string selector;
        public string theme;
        public PreviewViewport viewport;
    }

    [Serializable]
    internal sealed class PreviewViewport
    {
        public int width;
        public string height;
    }
}
