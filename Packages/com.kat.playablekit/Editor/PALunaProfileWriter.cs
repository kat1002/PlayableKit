#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor
{
    /// <summary>
    /// Writes luna.json for one playable profile from the project's profile registry (Tools/Playworks/PlayableProfiles.json).
    /// luna.json holds a single creative's scene, creative name and script excludes, and they must be written together or Luna compiles the wrong scripts.
    /// All validation happens in memory first; luna.json is written once, then verified. Why and how: Documentation~/BuildSelector.md.
    /// </summary>
    public static class PALunaProfileWriter
    {
        // Marker strings are unchanged so projects migrating from the legacy scripts keep their luna.json markers.
        public const string GeneratedExcludesBegin = "Assets/__generated_excludes_begin__/";
        public const string GeneratedExcludesEnd = "Assets/__generated_excludes_end__/";
        public const string ProfilesRelativePath = "Tools/Playworks/PlayableProfiles.json";
        public const string KeepListFolderRelativePath = "Tools/Playworks/creatives/";
        public const string WindowMenuPath = "Tools/PlayableKit/Luna/Build Selector";
        public const string FeatureArgument = "-playableFeature";
        public const string SceneArgument = "-playableScene";

        private const string LunaConfigRelativePath = "luna.json";
        private const string LogPrefix = "[PlayableKit Build Selector] ";

        private sealed class ProfileData
        {
            public string Feature;
            public string ScenePath;
            public string KeepListPath;
            public string CreativeName;
            public bool DiagnosticsOnly;
            public List<string> TextureNoCompression;
        }

        public static string GetProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        public static string GetProjectPath(string relative)
        {
            return Path.Combine(GetProjectRoot(), relative);
        }

        // ---- Script excludes. All comparisons are ordinal. ----

        public static void LoadKeepList(string absolutePath, out List<string> folders, out HashSet<string> files)
        {
            folders = new List<string>();
            files = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(absolutePath, Encoding.UTF8))
            {
                var entry = line.Trim().Replace('\\', '/');
                if (entry.Length == 0 || entry.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (entry.EndsWith("/", StringComparison.Ordinal))
                {
                    folders.Add(entry);
                }
                else
                {
                    files.Add(entry);
                }
            }
        }

        // Filesystem walk: AssetDatabase hides "."-prefixed folders that a plain directory walk includes.
        public static List<string> CollectRuntimeScripts()
        {
            var found = new List<string>();
            WalkScripts(Path.Combine(GetProjectRoot(), "Assets"), "Assets", found);
            return found;
        }

        private static void WalkScripts(string absoluteDirectory, string relativeDirectory, List<string> found)
        {
            foreach (var file in Directory.GetFiles(absoluteDirectory))
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith(".cs", StringComparison.Ordinal))
                {
                    found.Add(relativeDirectory + "/" + name);
                }
            }

            foreach (var directory in Directory.GetDirectories(absoluteDirectory))
            {
                var name = Path.GetFileName(directory);
                if (name == "Editor" || name.EndsWith("~", StringComparison.Ordinal))
                {
                    continue;
                }

                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                WalkScripts(directory, relativeDirectory + "/" + name, found);
            }
        }

        public static List<string> FindMissingKeepFiles(List<string> scripts, HashSet<string> keepFiles)
        {
            var scriptSet = new HashSet<string>(scripts, StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (var file in keepFiles)
            {
                if (!scriptSet.Contains(file))
                {
                    missing.Add(file);
                }
            }

            missing.Sort(StringComparer.Ordinal);
            return missing;
        }

        public static HashSet<string> ComputeExcludes(List<string> scripts, List<string> folders, HashSet<string> files)
        {
            var kept = new HashSet<string>(StringComparer.Ordinal);
            foreach (var script in scripts)
            {
                var isKept = files.Contains(script);
                for (var index = 0; !isKept && index < folders.Count; index++)
                {
                    isKept = script.StartsWith(folders[index], StringComparison.Ordinal);
                }

                if (isKept)
                {
                    kept.Add(script);
                }
            }

            var dirsWithKept = new HashSet<string>(StringComparer.Ordinal);
            foreach (var script in kept)
            {
                var parts = script.Split('/');
                for (var depth = 1; depth <= parts.Length - 1; depth++)
                {
                    dirsWithKept.Add(string.Join("/", parts, 0, depth));
                }
            }

            var excludes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var script in scripts)
            {
                if (kept.Contains(script))
                {
                    continue;
                }

                var parts = script.Split('/');
                var excluded = false;
                for (var depth = 2; depth <= parts.Length - 1; depth++)
                {
                    var folder = string.Join("/", parts, 0, depth);
                    if (!dirsWithKept.Contains(folder))
                    {
                        excludes.Add(folder + "/");
                        excluded = true;
                        break;
                    }
                }

                if (!excluded)
                {
                    excludes.Add(script);
                }
            }

            return excludes;
        }

        // Entries after the END marker move in front of the regenerated block, and there is no guard for END before
        // BEGIN. Kept for output compatibility with the legacy Python script, so migrating projects get identical luna.json bytes.
        public static List<string> BuildExpectedExcludes(IList<string> existing, IEnumerable<string> generated)
        {
            var begin = IndexOfOrdinal(existing, GeneratedExcludesBegin);
            var end = IndexOfOrdinal(existing, GeneratedExcludesEnd);
            var expected = new List<string>();
            if (begin >= 0 && end >= 0)
            {
                for (var index = 0; index < begin; index++)
                {
                    expected.Add(existing[index]);
                }

                for (var index = end + 1; index < existing.Count; index++)
                {
                    expected.Add(existing[index]);
                }
            }
            else
            {
                expected.AddRange(existing);
            }

            var sorted = new List<string>(generated);
            sorted.Sort(StringComparer.Ordinal);
            expected.Add(GeneratedExcludesBegin);
            expected.AddRange(sorted);
            expected.Add(GeneratedExcludesEnd);
            return expected;
        }

        // True when the existing excludes already equal what the keep list and current scripts would produce.
        public static bool ExcludesMatch(IList<string> existing, IEnumerable<string> generated)
        {
            var expected = BuildExpectedExcludes(existing, generated);
            if (expected.Count != existing.Count)
            {
                return false;
            }

            for (var index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(expected[index], existing[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        // ---- Prepare ----

        public static string ResolveFeatureForScene(string scenePath)
        {
            var matches = new List<string>();
            foreach (var profile in LoadProfiles())
            {
                if (!profile.DiagnosticsOnly &&
                    string.Equals(profile.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(profile.Feature);
                }
            }

            if (matches.Count == 0)
            {
                throw new InvalidOperationException("No buildable profile uses scene " + scenePath + ".");
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    "Profiles " + string.Join(", ", matches.ToArray()) + " all use " + scenePath +
                    ". Keep one entry in " + Path.GetFileName(ProfilesRelativePath) + ".");
            }

            return matches[0];
        }

        public static string Prepare(string feature)
        {
            var root = GetProjectRoot();
            var lunaPath = Path.Combine(root, LunaConfigRelativePath);
            if (!File.Exists(lunaPath))
            {
                throw new InvalidOperationException("Playworks configuration was not found: " + lunaPath);
            }

            var profile = FindProfile(LoadProfiles(), feature);
            var scenePath = profile.ScenePath;
            if (!File.Exists(Path.Combine(root, scenePath)))
            {
                throw new InvalidOperationException("Playable scene was not found: " + scenePath);
            }

            var keepPath = Path.Combine(root, profile.KeepListPath);
            if (!profile.DiagnosticsOnly && !File.Exists(keepPath))
            {
                throw new InvalidOperationException("Playable keep list was not found: " + keepPath);
            }

            var config = ReadJson(lunaPath);
            var unity = config["unity"] as JObject;
            var scripts = unity != null ? unity["scripts"] as JObject : null;
            var excludesArray = scripts != null ? scripts["excludes"] as JArray : null;
            if (excludesArray == null)
            {
                throw new InvalidOperationException("luna.json has no unity.scripts.excludes array.");
            }

            var existingExcludes = new List<string>();
            foreach (var item in excludesArray)
            {
                if (item.Type != JTokenType.String)
                {
                    throw new InvalidOperationException("luna.json unity.scripts.excludes contains a non-string entry.");
                }

                existingExcludes.Add((string)item);
            }

            var sceneArray = new JArray();
            sceneArray.Add(scenePath);
            unity["scenes"] = sceneArray;
            unity["startupScene"] = 0;
            config["creativeName"] = profile.CreativeName;

            HashSet<string> generated = null;
            if (!profile.DiagnosticsOnly)
            {
                List<string> keepFolders;
                HashSet<string> keepFiles;
                LoadKeepList(keepPath, out keepFolders, out keepFiles);
                var runtimeScripts = CollectRuntimeScripts();
                var missing = FindMissingKeepFiles(runtimeScripts, keepFiles);
                if (missing.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Failed to generate script excludes for " + profile.Feature +
                        ": Keep list names scripts that do not exist:\n  " + string.Join("\n  ", missing.ToArray()));
                }

                generated = ComputeExcludes(runtimeScripts, keepFolders, keepFiles);
                var newExcludes = new JArray();
                foreach (var entry in BuildExpectedExcludes(existingExcludes, generated))
                {
                    newExcludes.Add(entry);
                }

                scripts["excludes"] = newExcludes;
            }

            ApplyTextureRules(config, profile.TextureNoCompression);
            WriteJson(lunaPath, config);
            VerifyWritten(lunaPath, profile, generated);
            return "Selected " + profile.Feature + " playable: " + scenePath;
        }

        // Batch entry point: -executeMethod PlayableKit.Editor.PALunaProfileWriter.PrepareFromCommandLine
        // Exits 0 on success and 1 on any failure; the reason is in the log.
        public static void PrepareFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var featureArgument = GetArgumentValue(args, FeatureArgument);
                var sceneArgument = GetArgumentValue(args, SceneArgument);
                var hasFeature = featureArgument != null;
                var hasScene = sceneArgument != null;
                var value = hasFeature ? featureArgument : sceneArgument;
                if (hasFeature == hasScene ||
                    string.IsNullOrEmpty(value) ||
                    value.StartsWith("-", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Pass " + FeatureArgument + " <Feature> or " + SceneArgument + " <Assets/...unity>.");
                }

                var feature = hasFeature ? value : ResolveFeatureForScene(value);
                Debug.Log(LogPrefix + Prepare(feature));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        // Returns null when the flag is absent, "" when it is the last argument, else the following argument.
        private static string GetArgumentValue(string[] args, string flag)
        {
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return index + 1 < args.Length ? args[index + 1] : string.Empty;
                }
            }

            return null;
        }

        private static int IndexOfOrdinal(IList<string> list, string value)
        {
            for (var index = 0; index < list.Count; index++)
            {
                if (string.Equals(list[index], value, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        // ---- Registry ----

        private static List<ProfileData> LoadProfiles()
        {
            var path = GetProjectPath(ProfilesRelativePath);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Playable profile registry was not found: " + path);
            }

            var registry = ReadJson(path);
            var array = registry["profiles"] as JArray;
            if (array == null)
            {
                throw new InvalidOperationException(Path.GetFileName(ProfilesRelativePath) + " has no profiles array.");
            }

            var profiles = new List<ProfileData>();
            foreach (var item in array)
            {
                var entry = item as JObject;
                if (entry == null)
                {
                    continue;
                }

                var profile = new ProfileData
                {
                    Feature = TokenText(entry["feature"]) ?? string.Empty,
                    ScenePath = TokenText(entry["scenePath"]) ?? string.Empty,
                    KeepListPath = TokenText(entry["keepListPath"]) ?? string.Empty,
                    CreativeName = TokenText(entry["creativeName"]) ?? string.Empty,
                    DiagnosticsOnly = entry["diagnosticsOnly"] != null && entry["diagnosticsOnly"].Type == JTokenType.Boolean &&
                        (bool)entry["diagnosticsOnly"],
                    TextureNoCompression = new List<string>(),
                };

                var textures = entry["textureNoCompression"] as JArray;
                if (textures != null)
                {
                    foreach (var texture in textures)
                    {
                        var text = TokenText(texture);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            profile.TextureNoCompression.Add(text);
                        }
                    }
                }

                profiles.Add(profile);
            }

            return profiles;
        }

        private static ProfileData FindProfile(List<ProfileData> profiles, string feature)
        {
            foreach (var profile in profiles)
            {
                if (string.Equals(profile.Feature, feature, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            var features = new List<string>();
            foreach (var profile in profiles)
            {
                features.Add(profile.Feature);
            }

            features.Sort(StringComparer.OrdinalIgnoreCase);
            throw new InvalidOperationException(
                "Unknown playable profile '" + feature + "'. Available profiles: " + string.Join(", ", features.ToArray()));
        }

        // Missing/null -> null (callers coerce to ""), anything else its text.
        private static string TokenText(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            return token.ToString();
        }

        // ---- Textures ----

        private static void ApplyTextureRules(JObject config, List<string> texturePaths)
        {
            var texture = config.SelectToken("assets.rules.texture") as JObject;
            if (texture == null)
            {
                throw new InvalidOperationException("luna.json has no assets.rules.texture object.");
            }

            var existingOverrides = texture["overrides"] as JArray;
            if (existingOverrides == null)
            {
                throw new InvalidOperationException("luna.json has no assets.rules.texture.overrides array.");
            }

            var overrides = new JArray();
            foreach (var item in existingOverrides)
            {
                var entry = item as JObject;
                if (entry != null && !string.IsNullOrWhiteSpace(TokenText(entry["name"])))
                {
                    overrides.Add(entry);
                }
            }

            texture["overrides"] = overrides;

            foreach (var path in texturePaths)
            {
                JObject target = null;
                foreach (var item in overrides)
                {
                    var entry = (JObject)item;
                    if (string.Equals(TokenText(entry["name"]), path, StringComparison.OrdinalIgnoreCase))
                    {
                        target = entry;
                        break;
                    }
                }

                if (target == null)
                {
                    var defaults = texture["default"] as JObject;
                    if (defaults == null)
                    {
                        throw new InvalidOperationException("luna.json has no assets.rules.texture.default object.");
                    }

                    target = (JObject)defaults.DeepClone();
                    target["name"] = path;
                    overrides.Add(target);
                }

                target["compression"] = "none";
            }
        }

        // ---- Verification ----

        private static void VerifyWritten(string lunaPath, ProfileData profile, HashSet<string> generated)
        {
            var written = ReadJson(lunaPath);
            var unity = written["unity"] as JObject;
            var reasons = new List<string>();

            var scenes = unity != null ? unity["scenes"] as JArray : null;
            if (scenes == null || scenes.Count != 1 || scenes[0].Type != JTokenType.String ||
                !string.Equals((string)scenes[0], profile.ScenePath, StringComparison.Ordinal))
            {
                reasons.Add("unity.scenes is not [" + profile.ScenePath + "]");
            }

            var startup = unity != null ? unity["startupScene"] : null;
            if (startup == null || startup.Type != JTokenType.Integer || (long)startup != 0)
            {
                reasons.Add("unity.startupScene is not 0");
            }

            var creative = written["creativeName"];
            if (creative == null || creative.Type != JTokenType.String ||
                !string.Equals((string)creative, profile.CreativeName, StringComparison.Ordinal))
            {
                reasons.Add("creativeName is not '" + profile.CreativeName + "'");
            }

            if (!profile.DiagnosticsOnly)
            {
                var scripts = unity != null ? unity["scripts"] as JObject : null;
                var array = scripts != null ? scripts["excludes"] as JArray : null;
                var writtenExcludes = new List<string>();
                var allStrings = array != null;
                if (array != null)
                {
                    foreach (var item in array)
                    {
                        if (item.Type != JTokenType.String)
                        {
                            allStrings = false;
                            break;
                        }

                        writtenExcludes.Add((string)item);
                    }
                }

                if (!allStrings)
                {
                    reasons.Add("unity.scripts.excludes is not a string array");
                }
                else if (IndexOfOrdinal(writtenExcludes, GeneratedExcludesBegin) < 0 ||
                    IndexOfOrdinal(writtenExcludes, GeneratedExcludesEnd) < 0)
                {
                    reasons.Add("unity.scripts.excludes has no generated excludes markers");
                }
                else if (!ExcludesMatch(writtenExcludes, generated))
                {
                    reasons.Add("unity.scripts.excludes does not match the keep list");
                }
            }

            if (reasons.Count > 0)
            {
                throw new InvalidOperationException(
                    "Playable profile '" + profile.Feature + "' was written but verification failed: " +
                    string.Join("; ", reasons.ToArray()) +
                    ". Prepare it again in Tools > PlayableKit > Luna > Build Selector.");
            }
        }

        // ---- JSON I/O ----

        // DateParseHandling.None keeps ISO-date-like strings as strings; JObject.Parse defaults would rewrite them.
        private static JObject ReadJson(string path)
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            using (var reader = new JsonTextReader(new StringReader(text)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                var root = JToken.ReadFrom(reader) as JObject;
                if (root == null)
                {
                    throw new InvalidOperationException("JSON root is not an object: " + path);
                }

                return root;
            }
        }

        // Same bytes as Python json.dump(indent=4, ensure_ascii=False) + "\n": 4 spaces, LF, UTF-8 without BOM.
        private static void WriteJson(string path, JObject root)
        {
            var builder = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
            using (var writer = new JsonTextWriter(builder))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;
                writer.IndentChar = ' ';
                writer.StringEscapeHandling = StringEscapeHandling.Default;
                root.WriteTo(writer);
            }

            File.WriteAllText(path, builder + "\n", new UTF8Encoding(false));
        }
    }
}
#endif
