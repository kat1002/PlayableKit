#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor
{
    /// <summary>
    /// Exports the installed PlayableKit folder hierarchy as a standalone .unitypackage
    /// preserving all asset GUIDs and dependencies.
    /// </summary>
    public static class PAPackageExporter
    {
        private static readonly string[] DefaultCandidateRoots =
        {
            "Assets/PlayableKit"
        };

        [MenuItem("Tools/PlayableKit/Export PlayableKit.unitypackage")]
        public static void ExportPackageInteractive()
        {
            string defaultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlayableKit.unitypackage"));
            string outputPath = EditorUtility.SaveFilePanel(
                "Export PlayableKit Unity Package",
                Path.GetDirectoryName(defaultPath),
                "PlayableKit.unitypackage",
                "unitypackage");

            if (string.IsNullOrEmpty(outputPath)) return;
            ExportToPath(outputPath);
        }

        public static void ExportPackageBatchmode()
        {
            string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlayableKit.unitypackage"));
            bool ok = ExportToPath(outputPath);
            if (!ok)
            {
                EditorApplication.Exit(1);
            }
        }

        public static bool ExportToPath(string outputPath)
        {
            List<string> roots = new List<string>();
            for (int i = 0; i < DefaultCandidateRoots.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(DefaultCandidateRoots[i]))
                {
                    roots.Add(DefaultCandidateRoots[i]);
                }
            }

            if (roots.Count == 0)
            {
                Debug.LogError("[PlayableKit] Could not find PlayableKit folders under Assets/ to export.");
                return false;
            }

            AssetDatabase.ExportPackage(
                roots.ToArray(),
                outputPath,
                ExportPackageOptions.Recurse);

            Debug.Log("[PlayableKit] Exported Unity package (" + roots.Count + " root folder(s)) to: " + outputPath);
            return true;
        }
    }
}
#endif
