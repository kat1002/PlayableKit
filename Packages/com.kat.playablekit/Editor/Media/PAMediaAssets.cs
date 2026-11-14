#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor.Media
{
    /// <summary>Path helpers and the import settings PlayableKit applies to files the media tools write into Assets/.</summary>
    public static class PAMediaAssets
    {
        public static readonly string[] VideoExtensions = { ".mp4", ".mov", ".m4v", ".webm", ".avi", ".mkv" };

        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        public static string ToAbsolute(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot, assetPath));
        }

        /// <summary>"Assets/..." for a file inside the project's Assets folder, else null.</summary>
        public static string ToAssetPath(string absolutePath)
        {
            string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (!full.StartsWith(assets + "/", System.StringComparison.OrdinalIgnoreCase)) return null;
            return "Assets" + full.Substring(assets.Length);
        }

        public static bool IsVideoFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return System.Array.IndexOf(VideoExtensions, extension) >= 0;
        }

        /// <summary>Absolute paths of the video files selected in the Project window.</summary>
        public static List<string> SelectedVideoFiles()
        {
            var result = new List<string>();
            string[] guids = Selection.assetGUIDs;
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(assetPath) && IsVideoFile(assetPath)) result.Add(ToAbsolute(assetPath));
            }
            return result;
        }

        /// <summary>"name.mp4" → "name_1.mp4" ... until no file in <paramref name="paths"/> exists. Applies the same suffix to every path.</summary>
        public static string[] UniquePaths(params string[] paths)
        {
            string[] candidates = (string[])paths.Clone();
            for (int number = 1; AnyExists(candidates); number++)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    if (paths[i] == null) continue;
                    string folder = Path.GetDirectoryName(paths[i]) ?? string.Empty;
                    candidates[i] = Path.Combine(folder, Path.GetFileNameWithoutExtension(paths[i]) + "_" + number + Path.GetExtension(paths[i]));
                }
            }
            return candidates;
        }

        public static void DeleteIfExists(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                File.Delete(path);
            }
            catch (IOException e)
            {
                Debug.LogWarning("[PA][Media] Could not delete " + path + ": " + e.Message);
            }
        }

        /// <summary>Imports an encoded video as-is: no Unity transcoding, and no audio track when audio ships separately.</summary>
        public static void ImportVideo(string absolutePath, bool importAudio)
        {
            string assetPath = Import(absolutePath);
            var importer = assetPath == null ? null : AssetImporter.GetAtPath(assetPath) as VideoClipImporter;
            if (importer == null) return;

            VideoImporterTargetSettings settings = importer.defaultTargetSettings;
            settings.enableTranscoding = false;
            importer.defaultTargetSettings = settings;
            importer.importAudio = importAudio;
            importer.SaveAndReimport();
        }

        /// <summary>Imports a music / voice track with the Audio Import Optimizer's Music preset.</summary>
        public static void ImportMusic(string absolutePath)
        {
            string assetPath = Import(absolutePath);
            if (assetPath != null) PAAudioImportOptimizer.ApplyPreset(assetPath, false);
        }

        /// <summary>Imports a baked backdrop as a single, clamped, non-mipmapped sprite. Keeps a texture's existing settings when <paramref name="keepExisting"/>.</summary>
        public static void ImportBackdrop(string absolutePath, int longestSide, bool keepExisting)
        {
            string assetPath = Import(absolutePath);
            if (assetPath == null || keepExisting) return;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(longestSide), 32, 8192);
            importer.SaveAndReimport();
        }

        private static string Import(string absolutePath)
        {
            string assetPath = ToAssetPath(absolutePath);
            if (assetPath == null) return null;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return assetPath;
        }

        private static bool AnyExists(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] != null && File.Exists(paths[i])) return true;
            }
            return false;
        }
    }
}
#endif
