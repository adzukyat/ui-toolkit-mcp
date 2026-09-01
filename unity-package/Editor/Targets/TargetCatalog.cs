using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UIToolkitMcpPreviewServer.Protocol;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkitMcpPreviewServer.Targets
{
    internal sealed class ResolvedTarget
    {
        internal TargetInfo info;
        internal VisualTreeAsset document;
        internal EditorWindow window;
        internal PreviewDefinition preview;
    }

    internal static class TargetCatalog
    {
        internal static ListTargetsResult List(ListTargetsParameters parameters)
        {
            var configuration = PreviewConfigurationLoader.Load(out var configurationWarning);
            var targets = new List<TargetInfo>();
            var configuredPaths = new HashSet<string>(configuration.previews
                .Where(preview => preview != null && !string.IsNullOrEmpty(preview.document))
                .Select(preview => preview.document), StringComparer.Ordinal);

            foreach (var preview in configuration.previews)
            {
                if (preview == null || string.IsNullOrWhiteSpace(preview.alias) || string.IsNullOrWhiteSpace(preview.document))
                    continue;
                targets.Add(new TargetInfo
                {
                    kind = "preview",
                    id = "preview:" + preview.alias,
                    name = preview.alias,
                    path = preview.document,
                    type = nameof(VisualTreeAsset),
                    editorOnly = IsEditorDocument(preview.document),
                    configured = true,
                    initialization = "uxml-only"
                });
            }

            foreach (var guid in AssetDatabase.FindAssets("t:VisualTreeAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!parameters.includePackages && path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;
                targets.Add(new TargetInfo
                {
                    kind = "document",
                    id = "uxml:" + guid,
                    name = Path.GetFileNameWithoutExtension(path),
                    path = path,
                    type = nameof(VisualTreeAsset),
                    editorOnly = IsEditorDocument(path),
                    configured = configuredPaths.Contains(path),
                    initialization = "uxml-only"
                });
            }

            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window == null || window.rootVisualElement == null)
                    continue;
                targets.Add(new TargetInfo
                {
                    kind = "window",
                    id = "window:" + window.GetInstanceID(),
                    name = window.GetType().Name,
                    type = window.GetType().FullName,
                    title = window.titleContent?.text,
                    editorOnly = true,
                    initialization = "live"
                });
            }

            var query = parameters.query?.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                targets = targets.Where(target => Contains(target.id, query) || Contains(target.name, query) ||
                                                   Contains(target.path, query) || Contains(target.type, query) ||
                                                   Contains(target.title, query)).ToList();
            }

            targets.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
            return new ListTargetsResult
            {
                targets = targets.ToArray(),
                warnings = configurationWarning == null ? Array.Empty<string>() : new[] { configurationWarning }
            };
        }

        internal static ResolvedTarget Resolve(TargetReference reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.id))
                throw new ArgumentException("target.id is required.");

            if (reference.id.StartsWith("preview:", StringComparison.Ordinal))
            {
                var alias = reference.id.Substring("preview:".Length);
                var configuration = PreviewConfigurationLoader.Load(out _);
                var preview = configuration.previews.FirstOrDefault(item => item != null && item.alias == alias);
                if (preview == null)
                    throw new InvalidOperationException($"Preview alias '{alias}' was not found.");
                return ResolveDocument(preview.document, new TargetInfo
                {
                    kind = "preview",
                    id = reference.id,
                    name = alias,
                    path = preview.document,
                    type = nameof(VisualTreeAsset),
                    editorOnly = IsEditorDocument(preview.document),
                    configured = true,
                    initialization = "uxml-only"
                }, preview);
            }

            if (reference.id.StartsWith("uxml:", StringComparison.Ordinal))
            {
                var guid = reference.id.Substring("uxml:".Length);
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException($"VisualTreeAsset GUID '{guid}' was not found.");
                return ResolveDocument(path, new TargetInfo
                {
                    kind = "document",
                    id = reference.id,
                    name = Path.GetFileNameWithoutExtension(path),
                    path = path,
                    type = nameof(VisualTreeAsset),
                    editorOnly = IsEditorDocument(path),
                    initialization = "uxml-only"
                }, null);
            }

            if (reference.id.StartsWith("window:", StringComparison.Ordinal))
            {
                if (!int.TryParse(reference.id.Substring("window:".Length), out var instanceId))
                    throw new ArgumentException("Window target id is invalid.");
                var window = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(item => item.GetInstanceID() == instanceId);
                if (window == null)
                    throw new InvalidOperationException("The EditorWindow is no longer open.");
                return new ResolvedTarget
                {
                    window = window,
                    info = new TargetInfo
                    {
                        kind = "window",
                        id = reference.id,
                        name = window.GetType().Name,
                        type = window.GetType().FullName,
                        title = window.titleContent?.text,
                        editorOnly = true,
                        initialization = "live"
                    }
                };
            }

            if (reference.id.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase))
            {
                var path = reference.id;
                return ResolveDocument(path, new TargetInfo
                {
                    kind = "document",
                    id = "uxml:" + AssetDatabase.AssetPathToGUID(path),
                    name = Path.GetFileNameWithoutExtension(path),
                    path = path,
                    type = nameof(VisualTreeAsset),
                    editorOnly = IsEditorDocument(path),
                    initialization = "uxml-only"
                }, null);
            }

            throw new ArgumentException("target.id must be a preview alias, UXML id, window id, or .uxml asset path.");
        }

        private static ResolvedTarget ResolveDocument(string path, TargetInfo info, PreviewDefinition preview)
        {
            var document = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (document == null)
                throw new InvalidOperationException($"VisualTreeAsset was not found at '{path}'.");
            return new ResolvedTarget { document = document, info = info, preview = preview };
        }

        private static bool Contains(string value, string query)
        {
            return value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEditorDocument(string assetPath)
        {
            try
            {
                var absolute = ProjectPaths.Resolve(assetPath);
                if (!File.Exists(absolute))
                    return false;
                var text = File.ReadAllText(absolute);
                return text.IndexOf("UnityEditor.UIElements", StringComparison.Ordinal) >= 0 ||
                       text.IndexOf("editor-extension-mode=\"True\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       text.IndexOf("editor-extension-mode=\"true\"", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
