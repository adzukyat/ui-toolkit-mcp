using System;
using System.Diagnostics;
using System.IO;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UIToolkitMcpPreviewServer.Server
{
    [InitializeOnLoad]
    internal static class PreviewServerBootstrap
    {
        private static LoopbackServer _server;
        private static string EndpointDirectory => Path.Combine(ProjectPaths.Root, "Library", "UIToolkitMcpPreviewServer");
        internal static string EndpointPath => Path.Combine(EndpointDirectory, "endpoint.json");

        static PreviewServerBootstrap()
        {
            MainThreadDispatcher.Attach();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.delayCall += Start;
        }

        private static void Start()
        {
            if (_server != null)
                return;

            try
            {
                var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                _server = new LoopbackServer(token, PreviewRequestRouter.Handle);
                _server.Start();

                Directory.CreateDirectory(EndpointDirectory);
                var descriptor = new EndpointDescriptor
                {
                    protocolVersion = ProtocolVersion.Current,
                    port = _server.Port,
                    processId = Process.GetCurrentProcess().Id,
                    projectPath = ProjectPaths.Root,
                    unityVersion = Application.unityVersion,
                    token = token,
                    startedAtUtc = DateTime.UtcNow.ToString("O")
                };
                File.WriteAllText(EndpointPath, Json.Serialize(descriptor), new System.Text.UTF8Encoding(false));
                DiscoveryRegistry.Publish(descriptor);
            }
            catch (Exception exception)
            {
                _server?.Dispose();
                _server = null;
                UnityEngine.Debug.LogException(exception);
            }
        }

        private static void Stop()
        {
            _server?.Dispose();
            _server = null;
            MainThreadDispatcher.Detach();
            try
            {
                if (File.Exists(EndpointPath))
                    File.Delete(EndpointPath);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Could not remove UI Toolkit MCP preview endpoint: {exception.Message}");
            }
            try
            {
                DiscoveryRegistry.Remove(Process.GetCurrentProcess().Id);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Could not remove UI Toolkit MCP preview discovery entry: {exception.Message}");
            }
        }
    }
}
