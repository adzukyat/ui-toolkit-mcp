using System;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEditor;

namespace UIToolkitMcpPreviewServer.Server
{
    internal static class PreviewRequestRouter
    {
        internal static ResponseEnvelope Handle(RequestEnvelope request)
        {
            if (request == null)
                return ResponseEnvelope.Failure(null, "invalid_request", "Request is required.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return ResponseEnvelope.Failure(request.id, "unity_busy", "Unity is compiling scripts or updating the AssetDatabase. Retry shortly.");

            try
            {
                switch (request.method)
                {
                    case "status":
                        return ResponseEnvelope.Success(request.id, PreviewService.Status());
                    case "list_targets":
                        return ResponseEnvelope.Success(request.id, PreviewService.ListTargets(Json.Deserialize<ListTargetsParameters>(request.payload)));
                    case "inspect":
                        return ResponseEnvelope.Success(request.id, PreviewService.Inspect(Json.Deserialize<InspectParameters>(request.payload)));
                    case "screenshot":
                        return ResponseEnvelope.Success(request.id, PreviewService.Screenshot(Json.Deserialize<ScreenshotParameters>(request.payload)));
                    case "reload":
                        return ResponseEnvelope.Success(request.id, PreviewService.Reload(Json.Deserialize<ReloadParameters>(request.payload)));
                    default:
                        return ResponseEnvelope.Failure(request.id, "method_not_found", $"Unknown method '{request.method}'.");
                }
            }
            catch (ArgumentException exception)
            {
                return ResponseEnvelope.Failure(request.id, "invalid_arguments", exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return ResponseEnvelope.Failure(request.id, "unsupported", exception.Message, exception.ToString());
            }
            catch (Exception exception)
            {
                return ResponseEnvelope.Failure(request.id, "operation_failed", exception.Message, exception.ToString());
            }
        }
    }
}
