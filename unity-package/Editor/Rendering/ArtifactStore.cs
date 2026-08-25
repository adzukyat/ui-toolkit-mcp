using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UIToolkitMcpPreviewServer.Rendering
{
    internal static class ArtifactStore
    {
        private const long MaximumBytes = 100L * 1024L * 1024L;
        private static string DirectoryPath => Path.Combine(ProjectPaths.Root, "Library", "UIToolkitMcpPreviewServer", "artifacts");

        internal static string WritePng(byte[] png)
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, png);
            Trim();
            return Path.GetFullPath(path);
        }

        private static void Trim()
        {
            var files = new DirectoryInfo(DirectoryPath).GetFiles("*.png")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();
            long total = 0;
            foreach (var file in files)
            {
                total += file.Length;
                if (total <= MaximumBytes)
                    continue;
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Cache cleanup is best-effort and must not fail a capture.
                }
            }
        }
    }
}
