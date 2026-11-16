#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PlayableKit.Editor
{
    /// <summary>
    /// Keeps the kit listed in luna.json <c>unity.scripts.externalSources</c>.
    /// Luna compiles C# from Assets/ only; a package is compiled only when that list names its folder. Unity installs
    /// git packages under Library/PackageCache/com.kat.playablekit@&lt;hash&gt; and the hash changes on every update, so a
    /// stale entry makes Luna silently drop every PlayableKit component from the build. This runs after every domain
    /// reload (which follows any package install or update) and rewrites only the kit's own entry.
    /// </summary>
    [InitializeOnLoad]
    public static class PALunaExternalSources
    {
        public const string PackageName = "com.kat.playablekit";
        public const string MenuPath = "Tools/PlayableKit/Luna/Sync luna.json External Sources";
        private const string LunaJsonFile = "luna.json";
        private const string Key = "\"externalSources\"";

        static PALunaExternalSources()
        {
            EditorApplication.delayCall += () => Sync(false);
        }

        [MenuItem(MenuPath)]
        private static void SyncFromMenu()
        {
            Sync(true);
        }

        /// <summary>Updates luna.json when the kit's entry is missing or stale. Writes nothing otherwise.</summary>
        public static void Sync(bool verbose)
        {
            string kitPath = KitSourcePath();
            string jsonPath = LunaJsonPath();
            if (kitPath == null || !File.Exists(jsonPath))
            {
                if (verbose) Debug.Log("[PlayableKit] luna.json sync: nothing to do (no luna.json, or the kit is not installed as a package).");
                return;
            }

            string json = File.ReadAllText(jsonPath);
            if (!TryRewrite(json, kitPath, out string updated, out string error))
            {
                Debug.LogWarning("[PlayableKit] luna.json sync: " + error + " Add \"" + kitPath + "\" to unity.scripts.externalSources by hand, or Luna builds will drop every PlayableKit component.");
                return;
            }

            if (updated == json)
            {
                if (verbose) Debug.Log("[PlayableKit] luna.json already lists the kit: " + kitPath);
                return;
            }

            File.WriteAllText(jsonPath, updated, new UTF8Encoding(HasBom(jsonPath)));
            Debug.Log("[PlayableKit] luna.json externalSources now lists the kit at " + kitPath + " (Luna only compiles Assets/ on its own).");
        }

        /// <summary>
        /// Null when luna.json is fine or does not apply (no luna.json, kit copied into Assets/).
        /// Otherwise a message saying why Luna would build without the kit.
        /// </summary>
        public static string Problem()
        {
            string kitPath = KitSourcePath();
            string jsonPath = LunaJsonPath();
            if (kitPath == null || !File.Exists(jsonPath)) return null;

            string json = File.ReadAllText(jsonPath);
            if (!TryRewrite(json, kitPath, out string updated, out string error)) return "luna.json: " + error;
            if (updated == json) return null;
            return "luna.json externalSources does not list the kit's current folder (" + kitPath + "). Luna will build without PlayableKit and drop every PA component. Run " + MenuPath + ".";
        }

        /// <summary>
        /// Rewrites the externalSources array so it holds <paramref name="kitPath"/> once and no other
        /// com.kat.playablekit folder. Other entries and the rest of the file stay as they are. Returns false when the
        /// file has no externalSources array; <paramref name="result"/> equals <paramref name="json"/> when nothing changes.
        /// </summary>
        public static bool TryRewrite(string json, string kitPath, out string result, out string error)
        {
            result = json;
            error = null;

            int keyIndex = json.IndexOf(Key, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                error = "no \"externalSources\" key (open Luna's settings once so it writes the default luna.json).";
                return false;
            }

            int i = SkipWhitespace(json, keyIndex + Key.Length);
            if (i >= json.Length || json[i] != ':') { error = "\"externalSources\" is not followed by ':'."; return false; }
            i = SkipWhitespace(json, i + 1);
            if (i >= json.Length || json[i] != '[') { error = "\"externalSources\" is not an array."; return false; }
            int arrayStart = i;

            var entries = new List<string>();
            i = SkipWhitespace(json, i + 1);
            while (i < json.Length && json[i] != ']')
            {
                if (json[i] != '"') { error = "\"externalSources\" holds something other than strings."; return false; }
                if (!TryReadString(json, ref i, out string entry)) { error = "unterminated string in \"externalSources\"."; return false; }
                entries.Add(entry);
                i = SkipWhitespace(json, i);
                if (i < json.Length && json[i] == ',') i = SkipWhitespace(json, i + 1);
            }
            if (i >= json.Length) { error = "\"externalSources\" array is not closed."; return false; }
            int arrayEnd = i;

            var kept = new List<string>();
            bool hasCurrent = false;
            foreach (string entry in entries)
            {
                if (string.Equals(entry, kitPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasCurrent) kept.Add(entry);
                    hasCurrent = true;
                }
                else if (!IsKitFolder(entry))
                {
                    kept.Add(entry);
                }
            }
            if (!hasCurrent) kept.Add(kitPath);

            if (kept.Count == entries.Count && hasCurrent) return true;

            string newline = json.Contains("\r\n") ? "\r\n" : "\n";
            string indent = LineIndent(json, keyIndex);
            var sb = new StringBuilder("[");
            for (int k = 0; k < kept.Count; k++)
            {
                sb.Append(k == 0 ? newline : "," + newline);
                sb.Append(indent).Append("    ").Append(Quote(kept[k]));
            }
            sb.Append(newline).Append(indent).Append(']');

            result = json.Substring(0, arrayStart) + sb + json.Substring(arrayEnd + 1);
            return true;
        }

        /// <summary>True for any folder of this package: com.kat.playablekit or com.kat.playablekit@hash.</summary>
        public static bool IsKitFolder(string path)
        {
            string trimmed = path.TrimEnd('/', '\\');
            int slash = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
            string folder = trimmed.Substring(slash + 1);
            return folder.Equals(PackageName, StringComparison.OrdinalIgnoreCase)
                || folder.StartsWith(PackageName + "@", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The kit's folder the way Luna expects it: relative to the project (".\Library\...") when inside it.</summary>
        private static string KitSourcePath()
        {
            var info = PackageInfo.FindForAssembly(typeof(PALunaExternalSources).Assembly);
            if (info == null || string.IsNullOrEmpty(info.resolvedPath)) return null; // copied into Assets/: Luna compiles it already

            string root = Path.GetFullPath(ProjectRoot()).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(info.resolvedPath).TrimEnd('/', '\\');
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return full;
            return "." + Path.DirectorySeparatorChar + full.Substring(root.Length);
        }

        private static string ProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        private static string LunaJsonPath()
        {
            return Path.Combine(ProjectRoot(), LunaJsonFile);
        }

        private static bool HasBom(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return stream.Length >= 3 && stream.ReadByte() == 0xEF && stream.ReadByte() == 0xBB && stream.ReadByte() == 0xBF;
            }
        }

        private static int SkipWhitespace(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        private static string LineIndent(string s, int index)
        {
            int lineStart = s.LastIndexOf('\n', index) + 1;
            int i = lineStart;
            while (i < index && (s[i] == ' ' || s[i] == '\t')) i++;
            return s.Substring(lineStart, i - lineStart);
        }

        /// <summary>Reads a JSON string starting at the opening quote; leaves <paramref name="i"/> after the closing quote.</summary>
        private static bool TryReadString(string s, ref int i, out string value)
        {
            var sb = new StringBuilder();
            i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') { value = sb.ToString(); return true; }
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) { value = null; return false; }
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break; // \" \\ \/
                }
            }
            value = null;
            return false;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
#endif
