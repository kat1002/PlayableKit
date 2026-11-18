#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayableKit.Editor
{
    /// <summary>
    /// Prepares the playable scene that is open in the Editor and can register it in the profile registry.
    /// The profile registry is the source of truth; <see cref="PALunaProfileWriter"/> writes luna.json.
    /// luna.json holds one creative's scene, creative name and script excludes, and they must be written together or
    /// Luna compiles the wrong scripts. Why and how: Documentation~/BuildSelector.md.
    /// </summary>
    public sealed class PABuildSelectorWindow : EditorWindow
    {
        private const string PlayworksMenuPath = "Tools/Unity Playworks Plugin";
        private const string OpenPlayworksAfterReloadKey = "PlayableKit.BuildSelector.OpenPlayworksAfterReload";
        private const string LogPrefix = "[PlayableKit Build Selector] ";

        private static readonly string RegistryFileName = Path.GetFileName(PALunaProfileWriter.ProfilesRelativePath);

        [Serializable]
        private sealed class PlayableProfileRegistry
        {
            public PlayableProfile[] profiles;
        }

        [Serializable]
        private sealed class PlayableProfile
        {
            public string feature;
            public string displayName;
            public string scenePath;
            public string keepListPath;
            public string creativeName;
            public bool diagnosticsOnly;
            public string[] textureNoCompression;
        }

        private enum SceneState
        {
            Untitled,
            RegistryError,
            NotRegistered,
            DiagnosticsOnly,
            Duplicate,
            KeepListMissing,
            MissingScripts,
            NotPrepared,
            Stale,
            UpToDate
        }

        [Serializable]
        private sealed class LunaConfig
        {
            public string creativeName;
            public LunaUnityConfig unity;
        }

        [Serializable]
        private sealed class LunaUnityConfig
        {
            public string[] scenes;
            public int startupScene;
            public LunaScriptsConfig scripts;
        }

        [Serializable]
        private sealed class LunaScriptsConfig
        {
            public string[] excludes;
        }

        // Everything below is rebuilt by ReloadState; ReloadState is the only place that reads disk for the GUI.
        [NonSerialized] private PlayableProfile[] allProfiles = Array.Empty<PlayableProfile>();
        [NonSerialized] private PlayableProfile[] profiles = Array.Empty<PlayableProfile>();
        [NonSerialized] private string registryError;
        [NonSerialized] private bool registryEmpty;
        [NonSerialized] private string scenePath = string.Empty;
        [NonSerialized] private string sceneName = string.Empty;
        [NonSerialized] private SceneState state = SceneState.Untitled;
        [NonSerialized] private PlayableProfile profile;
        [NonSerialized] private string stateMessage;
        [NonSerialized] private string loadedScenePath;
        [NonSerialized] private bool keepListExists;
        [NonSerialized] private string registerKeepListPath;
        [NonSerialized] private bool registerKeepListExists;
        [NonSerialized] private string[] sourceLabels = { "Empty keep list" };
        [NonSerialized] private PlayableProfile[] sourceProfiles = { null };
        [NonSerialized] private int sourceIndex;
        [NonSerialized] private string status;
        [NonSerialized] private MessageType statusType = MessageType.Info;

        [MenuItem(PALunaProfileWriter.WindowMenuPath)]
        public static void ShowWindow()
        {
            GetWindow<PABuildSelectorWindow>("Build Selector");
        }

        private void OnEnable()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            ReloadState();
        }

        private void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
        }

        private void OnFocus()
        {
            ReloadState();
            Repaint();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ReloadState();
            Repaint();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ReloadState();
            Repaint();
        }

        private void OnSceneSaved(Scene scene)
        {
            ReloadState();
            Repaint();
        }

        private void RefreshState()
        {
            ReloadState();
            Repaint();
        }

        private bool StateAllowsPrepare()
        {
            return profile != null &&
                (state == SceneState.NotPrepared || state == SceneState.Stale || state == SceneState.UpToDate);
        }

        private static bool IsBusy()
        {
            return EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Build Selector", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Works on the scene open in the Editor. Prepare writes this scene, its creative name and its keep list to luna.json through the profile registry.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scene", string.IsNullOrEmpty(sceneName) ? "(untitled)" : sceneName);
            if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
            {
                EditorApplication.delayCall += RefreshState;
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(scenePath))
            {
                EditorGUILayout.SelectableLabel(scenePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            if (registryError != null)
            {
                EditorGUILayout.HelpBox(RegistryFileName + ": " + registryError, MessageType.Error);
            }

            if (profile != null)
            {
                EditorGUILayout.LabelField("Profile", profile.feature);
                EditorGUILayout.LabelField("Creative name", profile.creativeName);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    "Keep list",
                    string.IsNullOrWhiteSpace(profile.keepListPath) ? "(none)" : profile.keepListPath);
                using (new EditorGUI.DisabledScope(!keepListExists))
                {
                    if (GUILayout.Button("Open keep list", GUILayout.Width(100f)))
                    {
                        EditorUtility.OpenWithDefaultApp(PALunaProfileWriter.GetProjectPath(profile.keepListPath));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(stateMessage))
            {
                var prefix = state == SceneState.NotPrepared || state == SceneState.Stale || state == SceneState.UpToDate
                    ? "Luna status: "
                    : string.Empty;
                EditorGUILayout.HelpBox(prefix + stateMessage, GetStateMessageType());
            }

            if (state == SceneState.NotRegistered)
            {
                DrawRegisterBlock();
            }

            var canPrepare = StateAllowsPrepare() && !IsBusy();

            GUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(!canPrepare))
            {
                if (GUILayout.Button("Prepare This Scene", GUILayout.Height(28f)))
                {
                    PrepareCurrentScene(openPlayworks: false);
                }

                if (GUILayout.Button("Prepare and Open Playworks", GUILayout.Height(28f)))
                {
                    PrepareCurrentScene(openPlayworks: true);
                }
            }

            if (EditorApplication.isCompiling)
            {
                EditorGUILayout.HelpBox("Wait for Unity script compilation to finish before preparing a playable.", MessageType.Warning);
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Exit Play Mode before preparing a playable.", MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "After preparation, use Build Develop in the Unity Playworks window. Do not edit luna.json or run a keep/exclude script manually for a registered playable.",
                MessageType.None);

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.HelpBox(status, statusType);
            }
        }

        private void DrawRegisterBlock()
        {
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Register this scene", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Registers feature, display name and creative name " + sceneName + ", keep list " + registerKeepListPath + ".",
                MessageType.None);

            sourceIndex = EditorGUILayout.Popup("Start keep list from", sourceIndex, sourceLabels);

            if (registerKeepListExists)
            {
                EditorGUILayout.HelpBox(registerKeepListPath + " already exists and will be reused unchanged.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(IsBusy()))
            {
                if (GUILayout.Button("Register this scene"))
                {
                    var source = sourceIndex >= 0 && sourceIndex < sourceProfiles.Length ? sourceProfiles[sourceIndex] : null;
                    EditorApplication.delayCall += () =>
                    {
                        RegisterCurrentScene(source);
                        Repaint();
                    };
                }
            }
        }

        private MessageType GetStateMessageType()
        {
            switch (state)
            {
                case SceneState.UpToDate:
                case SceneState.NotPrepared:
                    return MessageType.Info;
                case SceneState.Stale:
                case SceneState.NotRegistered:
                case SceneState.Untitled:
                case SceneState.DiagnosticsOnly:
                    return MessageType.Warning;
                default:
                    return MessageType.Error;
            }
        }

        private void SetState(SceneState newState, string message)
        {
            state = newState;
            stateMessage = message;
        }

        private void ReloadState()
        {
            var newScenePath = EditorSceneManager.GetActiveScene().path ?? string.Empty;
            if (!string.Equals(newScenePath, loadedScenePath, StringComparison.Ordinal))
            {
                status = null;
                sourceIndex = 0;
            }

            loadedScenePath = newScenePath;

            scenePath = newScenePath;
            sceneName = Path.GetFileNameWithoutExtension(scenePath);
            profile = null;
            keepListExists = false;
            registerKeepListPath = null;
            registerKeepListExists = false;
            registryError = null;
            registryEmpty = false;
            allProfiles = Array.Empty<PlayableProfile>();
            profiles = Array.Empty<PlayableProfile>();
            sourceProfiles = new PlayableProfile[] { null };
            sourceLabels = new[] { "Empty keep list" };

            if (string.IsNullOrEmpty(scenePath))
            {
                SetState(SceneState.Untitled, "Save the scene first. The Build Selector prepares the scene open in the Editor.");
                return;
            }

            var all = new List<PlayableProfile>();
            var buildable = new List<PlayableProfile>();
            try
            {
                // A missing registry or one without profiles is not an error: Register creates the first profile.
                var registryPath = PALunaProfileWriter.GetProjectPath(PALunaProfileWriter.ProfilesRelativePath);
                var registry = File.Exists(registryPath)
                    ? JsonUtility.FromJson<PlayableProfileRegistry>(File.ReadAllText(registryPath))
                    : null;
                if (registry == null || registry.profiles == null || registry.profiles.Length == 0)
                {
                    registryEmpty = true;
                }
                else
                {
                    for (var index = 0; index < registry.profiles.Length; index++)
                    {
                        var entry = registry.profiles[index];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.feature) || string.IsNullOrWhiteSpace(entry.scenePath))
                        {
                            var message = "Profile #" + (index + 1) + " is missing feature or scenePath.";
                            registryError = registryError == null ? message : registryError + "\n" + message;
                            continue;
                        }

                        all.Add(entry);
                        if (!entry.diagnosticsOnly)
                        {
                            buildable.Add(entry);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                SetState(SceneState.RegistryError, RegistryFileName + " could not be read: " + exception.Message);
                return;
            }

            allProfiles = all.ToArray();
            profiles = buildable.ToArray();

            var sources = new List<PlayableProfile>(buildable);
            sources.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                Path.GetFileNameWithoutExtension(left.scenePath), Path.GetFileNameWithoutExtension(right.scenePath)));
            sourceProfiles = new PlayableProfile[sources.Count + 1];
            sourceLabels = new string[sources.Count + 1];
            sourceLabels[0] = "Empty keep list";
            for (var index = 0; index < sources.Count; index++)
            {
                sourceProfiles[index + 1] = sources[index];
                sourceLabels[index + 1] = Path.GetFileNameWithoutExtension(sources[index].scenePath);
            }

            sourceIndex = Mathf.Clamp(sourceIndex, 0, sourceProfiles.Length - 1);

            var buildableMatches = new List<PlayableProfile>();
            var diagnosticsMatches = new List<PlayableProfile>();
            foreach (var candidate in all)
            {
                if (!string.Equals(candidate.scenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (candidate.diagnosticsOnly)
                {
                    diagnosticsMatches.Add(candidate);
                }
                else
                {
                    buildableMatches.Add(candidate);
                }
            }

            if (buildableMatches.Count > 1)
            {
                var features = new List<string>();
                foreach (var match in buildableMatches)
                {
                    features.Add(match.feature);
                }

                SetState(
                    SceneState.Duplicate,
                    "Profiles " + string.Join(", ", features.ToArray()) + " all use " + sceneName +
                    ". Keep one entry in " + RegistryFileName + ".");
                return;
            }

            if (buildableMatches.Count == 0)
            {
                if (diagnosticsMatches.Count > 0)
                {
                    var feature = diagnosticsMatches[0].feature;
                    SetState(
                        SceneState.DiagnosticsOnly,
                        sceneName + " has only the diagnostics profile " + feature +
                        ". Diagnostics-only profiles are not prepared from this window. Use the batchmode command with -playableFeature " +
                        feature + " (see Documentation~/BuildSelector.md).");
                    return;
                }

                registerKeepListPath = PALunaProfileWriter.KeepListFolderRelativePath + sceneName + ".keep.txt";
                registerKeepListExists = File.Exists(PALunaProfileWriter.GetProjectPath(registerKeepListPath));
                SetState(
                    SceneState.NotRegistered,
                    registryEmpty
                        ? sceneName + " is not registered. No profile registry yet; Register creates " +
                            PALunaProfileWriter.ProfilesRelativePath + "."
                        : sceneName + " is not registered in " + RegistryFileName + ".");
                return;
            }

            profile = buildableMatches[0];

            if (string.IsNullOrWhiteSpace(profile.keepListPath) || !File.Exists(PALunaProfileWriter.GetProjectPath(profile.keepListPath)))
            {
                SetState(
                    SceneState.KeepListMissing,
                    "Keep list is missing: " + (string.IsNullOrWhiteSpace(profile.keepListPath) ? "(keepListPath not set)" : profile.keepListPath));
                return;
            }

            keepListExists = true;

            try
            {
                List<string> keepFolders;
                HashSet<string> keepFiles;
                PALunaProfileWriter.LoadKeepList(PALunaProfileWriter.GetProjectPath(profile.keepListPath), out keepFolders, out keepFiles);
                var scripts = PALunaProfileWriter.CollectRuntimeScripts();

                var missing = PALunaProfileWriter.FindMissingKeepFiles(scripts, keepFiles);

                if (missing.Count > 0)
                {
                    SetState(
                        SceneState.MissingScripts,
                        "Keep list names scripts that do not exist:\n  " + string.Join("\n  ", missing.ToArray()));
                    return;
                }

                var config = ReadLunaConfig();
                string[] scenes = config != null && config.unity != null ? config.unity.scenes : null;
                string[] excludes = config != null && config.unity != null && config.unity.scripts != null
                    ? config.unity.scripts.excludes
                    : null;
                var prepared = config != null && config.unity != null && scenes != null && scenes.Length == 1 &&
                    string.Equals(scenes[0], profile.scenePath, StringComparison.Ordinal) &&
                    config.unity.startupScene == 0 &&
                    string.Equals(config.creativeName, profile.creativeName, StringComparison.Ordinal) &&
                    excludes != null &&
                    Array.IndexOf(excludes, PALunaProfileWriter.GeneratedExcludesBegin) >= 0 &&
                    Array.IndexOf(excludes, PALunaProfileWriter.GeneratedExcludesEnd) >= 0;

                if (!prepared)
                {
                    var message = "Not prepared (luna.json uses " +
                        (scenes != null && scenes.Length > 0 ? Path.GetFileNameWithoutExtension(scenes[0]) : "unknown");
                    if (scenes != null && scenes.Length > 0 && string.Equals(scenes[0], profile.scenePath, StringComparison.Ordinal))
                    {
                        message += " with creative " + (string.IsNullOrEmpty(config.creativeName) ? "(none)" : config.creativeName);
                    }

                    SetState(SceneState.NotPrepared, message + ")");
                    return;
                }

                var begin = Array.IndexOf(excludes, PALunaProfileWriter.GeneratedExcludesBegin);
                var end = Array.IndexOf(excludes, PALunaProfileWriter.GeneratedExcludesEnd);
                if (end < begin)
                {
                    SetState(SceneState.NotPrepared, "Not prepared (luna.json has a malformed generated excludes block)");
                    return;
                }

                if (PALunaProfileWriter.ExcludesMatch(excludes, PALunaProfileWriter.ComputeExcludes(scripts, keepFolders, keepFiles)))
                {
                    SetState(SceneState.UpToDate, "Prepared, up to date");
                }
                else
                {
                    SetState(SceneState.Stale, "Keep list or scripts changed since the last Prepare; prepare again.");
                }
            }
            catch (IOException exception)
            {
                SetState(SceneState.KeepListMissing, "Keep list could not be read: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                SetState(SceneState.KeepListMissing, "Keep list could not be read: " + exception.Message);
            }
        }

        private static LunaConfig ReadLunaConfig()
        {
            try
            {
                var lunaPath = PALunaProfileWriter.GetProjectPath("luna.json");
                return JsonUtility.FromJson<LunaConfig>(File.ReadAllText(lunaPath));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("Unable to read the active Playworks profile: " + exception.Message);
                return null;
            }
        }

        private PlayableProfile GetActiveProfile()
        {
            var config = ReadLunaConfig();
            if (config == null || config.unity == null || config.unity.scenes == null || config.unity.scenes.Length != 1 ||
                config.unity.startupScene != 0 || config.unity.scripts == null || config.unity.scripts.excludes == null ||
                Array.IndexOf(config.unity.scripts.excludes, PALunaProfileWriter.GeneratedExcludesBegin) < 0 ||
                Array.IndexOf(config.unity.scripts.excludes, PALunaProfileWriter.GeneratedExcludesEnd) < 0)
            {
                return null;
            }

            foreach (var profile in profiles)
            {
                if (profile.scenePath == config.unity.scenes[0] && profile.creativeName == config.creativeName)
                {
                    return profile;
                }
            }

            return null;
        }

        private void PrepareCurrentScene(bool openPlayworks)
        {
            var target = profile;
            if (target == null)
            {
                return;
            }

            // Never change state or show a dialog inside the OnGUI handler.
            EditorApplication.delayCall += () => PrepareAfterSavePrompt(target, openPlayworks);
        }

        private void PrepareAfterSavePrompt(PlayableProfile target, bool openPlayworks)
        {
            var targetName = Path.GetFileNameWithoutExtension(target.scenePath);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                status = "Prepare cancelled; " + targetName + " was not saved.";
                statusType = MessageType.Warning;
                Repaint();
                return;
            }

            ReloadState();
            string problem = null;
            if (!string.Equals(scenePath, target.scenePath, StringComparison.OrdinalIgnoreCase))
            {
                problem = "the active scene is no longer " + targetName + ".";
            }
            else if (!StateAllowsPrepare())
            {
                problem = stateMessage;
            }
            else if (EditorApplication.isCompiling)
            {
                problem = "Unity is compiling scripts.";
            }
            else if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                problem = "Unity is in Play Mode.";
            }

            if (problem != null)
            {
                status = "Could not prepare " + targetName + ": " + problem;
                statusType = MessageType.Error;
                Repaint();
                return;
            }

            ClosePlayworksWindows();
            status = "Preparing " + targetName + "...";
            statusType = MessageType.Info;
            EditorApplication.delayCall += () => PrepareAfterPlayworksClosed(target, openPlayworks);
            Repaint();
        }

        private void PrepareAfterPlayworksClosed(PlayableProfile selected, bool openPlayworks)
        {
            try
            {
                PALunaProfileWriter.Prepare(selected.feature);

                AssetDatabase.Refresh();
                var activeProfile = GetActiveProfile();
                if (activeProfile == null || activeProfile.feature != selected.feature)
                {
                    throw new InvalidOperationException("luna.json was written but does not match the selected playable.");
                }

                status = "Prepared " + Path.GetFileNameWithoutExtension(selected.scenePath) + ". " + selected.scenePath;
                statusType = MessageType.Info;
                UnityEngine.Debug.Log(LogPrefix + status);

                // The Playworks plugin caches luna.json in static fields until a domain reload and writes that
                // cache back on build, which would overwrite the profile just written. Reload so it re-reads the file.
                if (openPlayworks)
                {
                    SessionState.SetBool(OpenPlayworksAfterReloadKey, true);
                }

                UnityEngine.Debug.Log(LogPrefix + "Prepared " + Path.GetFileNameWithoutExtension(selected.scenePath) +
                    "; reloading script domain so Playworks re-reads luna.json.");
                EditorUtility.RequestScriptReload();
            }
            catch (Exception exception)
            {
                status = "Could not prepare " + Path.GetFileNameWithoutExtension(selected.scenePath) + ": " + exception.Message;
                statusType = MessageType.Error;
                UnityEngine.Debug.LogError(LogPrefix + status);
                ReloadState();
                Repaint();
            }
        }

        private void RegisterCurrentScene(PlayableProfile source)
        {
            var name = sceneName;
            var path = scenePath;
            var keepRel = PALunaProfileWriter.KeepListFolderRelativePath + name + ".keep.txt";
            var keepAbs = PALunaProfileWriter.GetProjectPath(keepRel);
            var removeKeepListOnFailure = false;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new InvalidOperationException("The active scene is not saved.");
                }

                var registryAbs = PALunaProfileWriter.GetProjectPath(PALunaProfileWriter.ProfilesRelativePath);
                var registryExists = File.Exists(registryAbs);
                var text = registryExists ? File.ReadAllText(registryAbs) : string.Empty;
                var registry = registryExists ? JsonUtility.FromJson<PlayableProfileRegistry>(text) : null;
                var existingCount = registry != null && registry.profiles != null ? registry.profiles.Length : 0;
                var bootstrap = existingCount == 0;

                if (registry != null && registry.profiles != null)
                {
                    foreach (var existing in registry.profiles)
                    {
                        if (existing == null)
                        {
                            continue;
                        }

                        if (string.Equals(existing.scenePath, path, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("The scene is already used by profile " + existing.feature + ".");
                        }

                        if (string.Equals(existing.feature, name, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("The feature name " + existing.feature + " is already used.");
                        }
                    }
                }

                // A new registry is written with LF; an existing one keeps its own line endings.
                var registryNewline = bootstrap ? "\n" : text.Contains("\r\n") ? "\r\n" : "\n";
                string keepNote;
                if (File.Exists(keepAbs))
                {
                    keepNote = "already existed and was reused";
                }
                else
                {
                    string keepText;
                    if (source != null)
                    {
                        var sourceAbs = PALunaProfileWriter.GetProjectPath(source.keepListPath ?? string.Empty);
                        if (string.IsNullOrWhiteSpace(source.keepListPath) || !File.Exists(sourceAbs))
                        {
                            throw new FileNotFoundException("The keep list of " + source.feature + " is missing: " + source.keepListPath);
                        }

                        var sourceText = File.ReadAllText(sourceAbs);
                        var nl = sourceText.Contains("\r\n") ? "\r\n" : "\n";
                        var sourceName = Path.GetFileNameWithoutExtension(source.scenePath);
                        keepText = "# Keep list for " + name + " (" + path + ")." + nl +
                            "# Copied from the " + sourceName + " keep list (" + source.keepListPath + ") by the PlayableKit Build Selector." + nl +
                            sourceText;
                        if (sourceText.Length > 0 && !sourceText.EndsWith("\n", StringComparison.Ordinal))
                        {
                            keepText += nl;
                        }

                        keepNote = "created from " + sourceName;
                    }
                    else
                    {
                        var nl = registryNewline;
                        keepText = "# Keep list for " + name + " (" + path + ")." + nl +
                            "# One entry per line, relative to the Unity project root (the folder that contains Assets/):" + nl +
                            "#   Assets/Some/Folder/   keeps every runtime script below the folder" + nl +
                            "#   Assets/Some/File.cs   keeps one script" + nl +
                            "# Lines starting with # are comments. Every other runtime script under Assets/ is excluded from the Luna build." + nl;
                        keepNote = "created empty";
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(keepAbs));
                    File.WriteAllText(keepAbs, keepText, new UTF8Encoding(false));
                    removeKeepListOnFailure = true;
                }

                var newText = bootstrap
                    ? NewRegistry(registryNewline, name, path, keepRel, source)
                    : InsertProfile(text, registryNewline, name, path, keepRel, source);

                PlayableProfileRegistry parsed;
                try
                {
                    parsed = JsonUtility.FromJson<PlayableProfileRegistry>(newText);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException("The generated JSON is invalid: " + exception.Message);
                }

                if (parsed == null || parsed.profiles == null || parsed.profiles.Length != existingCount + 1)
                {
                    throw new InvalidDataException("The generated JSON is invalid: profile count is wrong.");
                }

                var added = parsed.profiles[parsed.profiles.Length - 1];
                if (added.feature != name || added.displayName != name || added.creativeName != name ||
                    added.scenePath != path || added.keepListPath != keepRel || added.diagnosticsOnly ||
                    !SameTextures(added.textureNoCompression, source == null ? null : source.textureNoCompression))
                {
                    throw new InvalidDataException("The generated JSON is invalid: the new profile does not match.");
                }

                removeKeepListOnFailure = false;
                Directory.CreateDirectory(Path.GetDirectoryName(registryAbs));
                File.WriteAllText(registryAbs, newText, new UTF8Encoding(false));

                status = "Registered " + name + ". Keep list " + keepRel + " " + keepNote + ". Edit it if needed, then Prepare.";
                statusType = MessageType.Info;
                UnityEngine.Debug.Log(LogPrefix + status);
            }
            catch (Exception exception)
            {
                if (removeKeepListOnFailure)
                {
                    try
                    {
                        File.Delete(keepAbs);
                    }
                    catch (Exception deleteException)
                    {
                        UnityEngine.Debug.LogWarning(LogPrefix + "Could not remove " + keepRel + ": " + deleteException.Message);
                    }
                }

                status = "Could not register " + name + ": " + exception.Message;
                statusType = MessageType.Error;
                UnityEngine.Debug.LogError(LogPrefix + status);
            }
            finally
            {
                ReloadState();
            }
        }

        // Creates the registry for the first profile; used when the file is missing or lists no profiles.
        private static string NewRegistry(string nl, string name, string path, string keepRel, PlayableProfile source)
        {
            return "{" + nl + "  \"profiles\": [" + nl + BuildProfileObject(nl, name, path, keepRel, source) + nl + "  ]" + nl + "}" + nl;
        }

        // Appends one object after the last profile by text insertion so the rest of the file stays byte-identical.
        private static string InsertProfile(string text, string nl, string name, string path, string keepRel, PlayableProfile source)
        {
            var rootEnd = text.LastIndexOf('}');
            var arrayEnd = rootEnd < 0 ? -1 : text.LastIndexOf(']', rootEnd);
            var lastObjectEnd = arrayEnd <= 0 ? -1 : text.LastIndexOf('}', arrayEnd);
            if (arrayEnd <= 0 || lastObjectEnd < 0 || lastObjectEnd <= text.LastIndexOf('[', arrayEnd))
            {
                throw new InvalidDataException("Unexpected " + RegistryFileName + " layout.");
            }

            return text.Insert(lastObjectEnd + 1, "," + nl + BuildProfileObject(nl, name, path, keepRel, source));
        }

        private static string BuildProfileObject(string nl, string name, string path, string keepRel, PlayableProfile source)
        {
            var builder = new StringBuilder();
            builder.Append("    {").Append(nl);
            builder.Append("      \"feature\": \"").Append(EscapeJson(name)).Append("\",").Append(nl);
            builder.Append("      \"displayName\": \"").Append(EscapeJson(name)).Append("\",").Append(nl);
            builder.Append("      \"scenePath\": \"").Append(EscapeJson(path)).Append("\",").Append(nl);
            builder.Append("      \"keepListPath\": \"").Append(EscapeJson(keepRel)).Append("\",").Append(nl);
            builder.Append("      \"creativeName\": \"").Append(EscapeJson(name)).Append('"');

            if (source != null && source.textureNoCompression != null && source.textureNoCompression.Length > 0)
            {
                builder.Append(',').Append(nl).Append("      \"textureNoCompression\": [").Append(nl);
                for (var index = 0; index < source.textureNoCompression.Length; index++)
                {
                    builder.Append("        \"").Append(EscapeJson(source.textureNoCompression[index])).Append('"');
                    builder.Append(index < source.textureNoCompression.Length - 1 ? "," : string.Empty).Append(nl);
                }

                builder.Append("      ]");
            }

            builder.Append(nl).Append("    }");
            return builder.ToString();
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool SameTextures(string[] actual, string[] expected)
        {
            var actualLength = actual == null ? 0 : actual.Length;
            var expectedLength = expected == null ? 0 : expected.Length;
            if (actualLength != expectedLength)
            {
                return false;
            }

            for (var index = 0; index < actualLength; index++)
            {
                if (!string.Equals(actual[index], expected[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        [InitializeOnLoadMethod]
        private static void OpenPlayworksAfterReload()
        {
            if (!SessionState.GetBool(OpenPlayworksAfterReloadKey, false))
            {
                return;
            }

            SessionState.SetBool(OpenPlayworksAfterReloadKey, false);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.ExecuteMenuItem(PlayworksMenuPath))
                {
                    UnityEngine.Debug.LogWarning(LogPrefix + "Unity could not open " + PlayworksMenuPath + " automatically.");
                }
            };
        }

        private static void ClosePlayworksWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var typeName = window.GetType().FullName ?? string.Empty;
                var title = window.titleContent == null ? string.Empty : window.titleContent.text;
                if (typeName.IndexOf("Playworks", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("Playworks", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    window.Close();
                }
            }
        }
    }
}
#endif
