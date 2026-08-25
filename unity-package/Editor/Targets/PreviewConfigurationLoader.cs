using System;
using System.IO;
using UIToolkitMcpPreviewServer.Protocol;

namespace UIToolkitMcpPreviewServer.Targets
{
    internal static class PreviewConfigurationLoader
    {
        internal static string ConfigurationPath => Path.Combine(ProjectPaths.Root, ".ui-toolkit-mcp-preview.json");

        internal static PreviewConfiguration Load(out string warning)
        {
            warning = null;
            if (!File.Exists(ConfigurationPath))
                return new PreviewConfiguration { schemaVersion = 1, previews = Array.Empty<PreviewDefinition>() };

            try
            {
                var configuration = Json.Deserialize<PreviewConfiguration>(File.ReadAllText(ConfigurationPath));
                if (configuration == null || configuration.schemaVersion != 1)
                {
                    warning = "Ignoring .ui-toolkit-mcp-preview.json because schemaVersion is not 1.";
                    return new PreviewConfiguration { schemaVersion = 1, previews = Array.Empty<PreviewDefinition>() };
                }

                configuration.previews = configuration.previews ?? Array.Empty<PreviewDefinition>();
                return configuration;
            }
            catch (Exception exception)
            {
                warning = $"Could not read .ui-toolkit-mcp-preview.json: {exception.Message}";
                return new PreviewConfiguration { schemaVersion = 1, previews = Array.Empty<PreviewDefinition>() };
            }
        }
    }
}
