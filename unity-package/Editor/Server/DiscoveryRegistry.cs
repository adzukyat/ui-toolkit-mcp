using System;
using System.Diagnostics;
using System.IO;
using UIToolkitMcpPreviewServer.Protocol;

namespace UIToolkitMcpPreviewServer.Server
{
    internal static class DiscoveryRegistry
    {
        private static string DirectoryPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UIToolkitMcpPreviewServer",
            "endpoints");

        internal static string DescriptorPath(int processId) => Path.Combine(DirectoryPath, processId + ".json");

        internal static void Publish(EndpointDescriptor endpoint)
        {
            Directory.CreateDirectory(DirectoryPath);
            RemoveStaleEntries();
            var descriptor = new DiscoveryDescriptor
            {
                protocolVersion = endpoint.protocolVersion,
                processId = endpoint.processId,
                projectPath = endpoint.projectPath,
                projectName = new DirectoryInfo(endpoint.projectPath).Name,
                endpointPath = PreviewServerBootstrap.EndpointPath,
                unityVersion = endpoint.unityVersion,
                startedAtUtc = endpoint.startedAtUtc
            };
            var destination = DescriptorPath(endpoint.processId);
            var temporary = destination + ".tmp";
            File.WriteAllText(temporary, Json.Serialize(descriptor), new System.Text.UTF8Encoding(false));
            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(temporary, destination);
        }

        internal static void Remove(int processId)
        {
            var path = DescriptorPath(processId);
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void RemoveStaleEntries()
        {
            foreach (var path in Directory.GetFiles(DirectoryPath, "*.json"))
            {
                try
                {
                    var descriptor = Json.Deserialize<DiscoveryDescriptor>(File.ReadAllText(path));
                    if (descriptor.processId <= 0 || Process.GetProcessById(descriptor.processId).HasExited)
                        File.Delete(path);
                }
                catch
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        // Cleanup is best-effort; discovery validates every entry when it reads it.
                    }
                }
            }
        }
    }
}
