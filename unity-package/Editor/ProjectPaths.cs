using System.IO;
using UnityEngine;

namespace UIToolkitMcpPreviewServer
{
    internal static class ProjectPaths
    {
        internal static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        internal static string Resolve(string path)
        {
            return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(Root, path));
        }
    }
}
