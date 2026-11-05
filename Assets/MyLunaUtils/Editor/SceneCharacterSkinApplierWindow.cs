using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SceneCharacterSkinApplierWindow : EditorWindow
{
    private const string UndoName = "Apply Scene Character Skin";

    private readonly List<CharacterUsage> characterUsages = new List<CharacterUsage>();
    private readonly List<StaticAvatarImageUsage> sceneAvatarUsages = new List<StaticAvatarImageUsage>();
    private readonly List<RuntimeAssetWarning> runtimeWarnings = new List<RuntimeAssetWarning>();

    private ScriptableObject targetSkeletonDataAsset;
    private Sprite targetAvatarSprite;
    private string targetSkinName = "";
    private Vector2 scrollPosition;
    private bool showSkippedSpines;
    private bool validateSkinName = true;

    [MenuItem("Tools/MyLunaHelper/Scene Character Skin Applier")]
    public static void Open()
    {
        GetWindow<SceneCharacterSkinApplierWindow>("Character Skin Applier");
    }

    private void OnGUI()
    {
        var activeScene = SceneManager.GetActiveScene();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Scene Character Skin Applier", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Active Scene", string.IsNullOrEmpty(activeScene.path) ? activeScene.name : activeScene.path);

        EditorGUILayout.Space(8);
        targetSkeletonDataAsset = (ScriptableObject)EditorGUILayout.ObjectField(
            "Skeleton Data Asset",
            targetSkeletonDataAsset,
            typeof(ScriptableObject),
            false);
        targetSkinName = EditorGUILayout.TextField("Skin Name", targetSkinName);
        targetAvatarSprite = (Sprite)EditorGUILayout.ObjectField(
            "Avatar Sprite (optional)",
            targetAvatarSprite,
            typeof(Sprite),
            false);
        validateSkinName = EditorGUILayout.ToggleLeft("Validate skin exists before apply", validateSkinName);

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan Active Scene", GUILayout.Height(28)))
            {
                ScanActiveScene();
            }

            using (new EditorGUI.DisabledScope(!HasSelectedSceneApplyTarget()))
            {
                if (GUILayout.Button("Apply To Selected", GUILayout.Height(28)))
                {
                    ApplyToSelected();
                }
            }
        }

        using (new EditorGUI.DisabledScope(runtimeWarnings.All(x => !x.Selected)))
        {
            if (GUILayout.Button("Apply To Selected Runtime Warning Prefabs", GUILayout.Height(24)))
            {
                ApplyToSelectedRuntimeWarningPrefabs();
                GUIUtility.ExitGUI();
            }
        }

        showSkippedSpines = EditorGUILayout.ToggleLeft("Show skipped Spine objects", showSkippedSpines);

        DrawSummary();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawRuntimeWarnings();
        DrawCharacterUsages();
        DrawSceneAvatarUsages();
        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary()
    {
        int selected = characterUsages.Count(x => x.Selected && !x.Skipped);
        int candidates = characterUsages.Count(x => !x.Skipped);
        int skipped = characterUsages.Count(x => x.Skipped);
        int avatarCandidates = sceneAvatarUsages.Count;
        int selectedAvatars = sceneAvatarUsages.Count(x => x.Selected);
        int selectedRuntimeWarnings = runtimeWarnings.Count(x => x.Selected);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            $"Spine candidates: {candidates} | Selected Spine: {selected} | Scene avatar Images: {avatarCandidates} | Selected avatar Images: {selectedAvatars} | Skipped Spine objects: {skipped} | Runtime asset warnings: {runtimeWarnings.Count} | Selected runtime warnings: {selectedRuntimeWarnings}",
            runtimeWarnings.Count > 0 ? MessageType.Warning : MessageType.Info);
    }

    private void DrawRuntimeWarnings()
    {
        if (runtimeWarnings.Count == 0) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Runtime Asset Warnings (optional project asset changes)", EditorStyles.boldLabel);

        RuntimeAssetWarning warningToApply = null;
        foreach (var warning in runtimeWarnings.ToList())
        {
            EditorGUILayout.BeginVertical("box");
            warning.Selected = EditorGUILayout.ToggleLeft("Apply this runtime prefab", warning.Selected);
            EditorGUILayout.LabelField("Scene reference", warning.SceneObjectPath);
            EditorGUILayout.LabelField("Property", warning.PropertyPath);

            if (!string.IsNullOrEmpty(warning.SourceAssetPath))
            {
                EditorGUILayout.LabelField("Source asset", warning.SourceAssetPath);
            }

            EditorGUILayout.LabelField("Referenced prefab", warning.ReferencedAssetPath);
            EditorGUILayout.HelpBox(warning.Detail, MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (warning.SourceAsset != null && GUILayout.Button("Ping Source Asset", GUILayout.Width(130)))
                {
                    PingObject(warning.SourceAsset);
                }

                if (warning.ReferencedAsset != null && GUILayout.Button("Ping Prefab", GUILayout.Width(100)))
                {
                    PingObject(warning.ReferencedAsset);
                }

                using (new EditorGUI.DisabledScope(!CanApplyRuntimeWarning(warning)))
                {
                    if (GUILayout.Button("Apply To This Prefab", GUILayout.Width(140)))
                    {
                        warningToApply = warning;
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        if (warningToApply != null)
        {
            ApplyToRuntimeWarningPrefab(warningToApply);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawCharacterUsages()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Scene Character Candidates", EditorStyles.boldLabel);

        if (characterUsages.Count == 0)
        {
            EditorGUILayout.HelpBox("No scan result. Click Scan Active Scene.", MessageType.Info);
            return;
        }

        foreach (var usage in characterUsages)
        {
            if (usage.Skipped && !showSkippedSpines) continue;

            EditorGUILayout.BeginVertical("box");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(usage.Skipped))
                {
                    usage.Selected = EditorGUILayout.Toggle(usage.Selected, GUILayout.Width(18));
                }

                EditorGUILayout.LabelField(usage.ObjectPath, EditorStyles.boldLabel);
            }

            EditorGUILayout.LabelField("Component", usage.ComponentType);
            EditorGUILayout.LabelField("Current skeleton", usage.SkeletonAssetPath);
            EditorGUILayout.LabelField("Current skin", string.IsNullOrEmpty(usage.InitialSkinName) ? "<empty>" : usage.InitialSkinName);
            EditorGUILayout.HelpBox(usage.Reason, usage.Skipped ? MessageType.None : MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Object", GUILayout.Width(100)))
                {
                    Selection.activeGameObject = usage.GameObject;
                    EditorGUIUtility.PingObject(usage.GameObject);
                }

                if (usage.SkeletonDataAsset != null && GUILayout.Button("Ping Skeleton", GUILayout.Width(100)))
                {
                    PingObject(usage.SkeletonDataAsset);
                }
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawSceneAvatarUsages()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Scene Avatar Image Candidates", EditorStyles.boldLabel);

        if (sceneAvatarUsages.Count == 0)
        {
            EditorGUILayout.HelpBox("No static Image avatar found in active scene.", MessageType.Info);
            return;
        }

        foreach (var usage in sceneAvatarUsages)
        {
            EditorGUILayout.BeginVertical("box");

            using (new EditorGUILayout.HorizontalScope())
            {
                usage.Selected = EditorGUILayout.Toggle(usage.Selected, GUILayout.Width(18));
                EditorGUILayout.LabelField(usage.ObjectPath, EditorStyles.boldLabel);
            }

            EditorGUILayout.LabelField("Component", "UnityEngine.UI.Image");
            EditorGUILayout.LabelField("Current sprite", usage.SpritePath);
            EditorGUILayout.HelpBox(usage.Reason, MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Object", GUILayout.Width(100)))
                {
                    Selection.activeGameObject = usage.GameObject;
                    EditorGUIUtility.PingObject(usage.GameObject);
                }

                if (usage.Sprite != null && GUILayout.Button("Ping Sprite", GUILayout.Width(100)))
                {
                    PingObject(usage.Sprite);
                }
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void ScanActiveScene()
    {
        characterUsages.Clear();
        sceneAvatarUsages.Clear();
        runtimeWarnings.Clear();

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "Active scene is not valid or not loaded.", "OK");
            return;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "SkeletonGraphic" || typeName == "SkeletonAnimation")
                {
                    ReadSpineComponentData(mb, out Object skelAsset, out string initSkin);
                    AddUsage(mb, skelAsset, initSkin, typeName);
                }
            }

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                AddSceneAvatarUsage(image);
            }
        }

        ScanRuntimeAssetWarnings(scene);
        Repaint();
    }

    private static void ReadSpineComponentData(MonoBehaviour mb, out Object skeletonDataAsset, out string initialSkinName)
    {
        skeletonDataAsset = null;
        initialSkinName = "";
        if (mb == null) return;
        SerializedObject so = new SerializedObject(mb);
        SerializedProperty skelProp = so.FindProperty("skeletonDataAsset") ?? so.FindProperty("m_SkeletonDataAsset");
        SerializedProperty skinProp = so.FindProperty("initialSkinName") ?? so.FindProperty("_initialSkinName");
        if (skelProp != null) skeletonDataAsset = skelProp.objectReferenceValue;
        if (skinProp != null) initialSkinName = skinProp.stringValue;
    }

    private void AddUsage(Component component, Object skeletonDataAsset, string initialSkinName, string componentType)
    {
        string objectPath = GetHierarchyPath(component.gameObject);
        string skeletonPath = skeletonDataAsset != null ? AssetDatabase.GetAssetPath(skeletonDataAsset) : "";
        var classification = ClassifyCharacterUsage(objectPath, skeletonPath);

        characterUsages.Add(new CharacterUsage
        {
            Component = component,
            GameObject = component.gameObject,
            ComponentType = componentType,
            ObjectPath = objectPath,
            SkeletonDataAsset = skeletonDataAsset,
            SkeletonAssetPath = string.IsNullOrEmpty(skeletonPath) ? "<missing>" : skeletonPath,
            InitialSkinName = initialSkinName,
            Selected = classification.IsCharacter,
            Skipped = !classification.IsCharacter,
            Reason = classification.Reason
        });
    }

    private void AddSceneAvatarUsage(Image image)
    {
        if (image == null || !IsStaticAvatarImage(image.gameObject, image.sprite)) return;

        string spritePath = image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : "";
        sceneAvatarUsages.Add(new StaticAvatarImageUsage
        {
            Image = image,
            GameObject = image.gameObject,
            Sprite = image.sprite,
            ObjectPath = GetHierarchyPath(image.gameObject),
            SpritePath = string.IsNullOrEmpty(spritePath) ? "<missing>" : spritePath,
            Selected = false,
            Reason = "Static UI avatar Image candidate. Review and tick manually; Spine skin change cannot update this."
        });
    }

    private void ApplyToSelected()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "Active scene is not valid or not loaded.", "OK");
            return;
        }

        var selectedUsages = characterUsages.Where(x => x.Selected && !x.Skipped && x.Component != null).ToList();
        var selectedAvatars = sceneAvatarUsages.Where(x => x.Selected && x.Image != null).ToList();
        if (selectedUsages.Count == 0 && selectedAvatars.Count == 0)
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "No selected scene character or avatar candidate.", "OK");
            return;
        }

        if (selectedUsages.Count > 0 && !ValidateTargetInput())
        {
            return;
        }

        if (selectedAvatars.Count > 0 && !ValidateAvatarTargetInput())
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        foreach (var usage in selectedUsages)
        {
            Undo.RecordObject(usage.Component, UndoName);
            ApplyToComponent(usage.Component);
            EditorUtility.SetDirty(usage.Component);

            if (PrefabUtility.IsPartOfPrefabInstance(usage.Component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(usage.Component);
            }
        }

        foreach (var usage in selectedAvatars)
        {
            Undo.RecordObject(usage.Image, UndoName);
            usage.Image.sprite = targetAvatarSprite;
            EditorUtility.SetDirty(usage.Image);

            if (PrefabUtility.IsPartOfPrefabInstance(usage.Image))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(usage.Image);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        ScanActiveScene();

        Debug.Log($"[SceneCharacterSkinApplier] Applied {selectedUsages.Count} Spine object(s) and {selectedAvatars.Count} avatar Image(s) in {scene.path}.");
    }

    private void ApplyToRuntimeWarningPrefab(RuntimeAssetWarning warning)
    {
        if (!ValidateRuntimeWarningInput(new[] { warning }))
        {
            return;
        }

        bool applySpine = warning.HasSpineCandidates && HasSkeletonTargetInput();
        bool applyAvatar = warning.HasStaticAvatarCandidates && targetAvatarSprite != null;
        string prefabPath = AssetDatabase.GetAssetPath(warning.ReferencedAsset);
        if (string.IsNullOrEmpty(prefabPath))
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "Referenced prefab path is missing.", "OK");
            return;
        }

        string targetSummary = BuildApplyTargetSummary(applySpine, applyAvatar, warning.HasStaticAvatarCandidates);

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply to project prefab?",
            $"This will modify a shared project asset, not only the open scene.\n\nPrefab:\n{prefabPath}\n\nSource:\n{warning.SourceAssetPath}\n\nApply target(s) inside this prefab?\n{targetSummary}",
            "Apply",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        int appliedCount = ApplyToPrefabAsset(prefabPath, applySpine, applyAvatar);
        if (appliedCount <= 0)
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", $"No character/avatar candidate found in prefab:\n{prefabPath}", "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ScanActiveScene();

        Debug.Log($"[SceneCharacterSkinApplier] Applied {appliedCount} character/avatar object(s) in prefab asset {prefabPath}.");
    }

    private void ApplyToSelectedRuntimeWarningPrefabs()
    {
        var selectedWarnings = runtimeWarnings.Where(x => x.Selected).ToList();
        if (selectedWarnings.Count == 0)
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "No selected runtime prefab warning.", "OK");
            return;
        }

        if (!ValidateRuntimeWarningInput(selectedWarnings))
        {
            return;
        }

        bool applySpine = selectedWarnings.Any(x => x.HasSpineCandidates) && HasSkeletonTargetInput();
        bool applyAvatar = selectedWarnings.Any(x => x.HasStaticAvatarCandidates) && targetAvatarSprite != null;
        var prefabPaths = selectedWarnings
            .Select(x => AssetDatabase.GetAssetPath(x.ReferencedAsset))
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();

        if (prefabPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "Selected runtime warnings have no prefab path.", "OK");
            return;
        }

        string prefabList = string.Join("\n", prefabPaths);
        string targetSummary = BuildApplyTargetSummary(applySpine, applyAvatar, selectedWarnings.Any(x => x.HasStaticAvatarCandidates));

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply to selected project prefabs?",
            $"This will modify shared project assets, not only the open scene.\n\nPrefabs:\n{prefabList}\n\nApply target(s) inside these prefabs?\n{targetSummary}",
            "Apply",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        int appliedCount = 0;
        foreach (string prefabPath in prefabPaths)
        {
            appliedCount += ApplyToPrefabAsset(prefabPath, applySpine, applyAvatar);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ScanActiveScene();

        Debug.Log($"[SceneCharacterSkinApplier] Applied {appliedCount} character/avatar object(s) in {prefabPaths.Count} runtime prefab asset(s).");
    }

    private int ApplyToPrefabAsset(string prefabPath, bool applySpine, bool applyAvatar)
    {
        int appliedCount = 0;

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var prefabRoot = scope.prefabContentsRoot;
            if (prefabRoot == null) return 0;

            if (applySpine)
            {
                foreach (var mb in prefabRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName != "SkeletonGraphic" && typeName != "SkeletonAnimation") continue;

                    ReadSpineComponentData(mb, out Object skelAsset, out _);
                    if (!IsCharacterComponent(mb.gameObject, skelAsset)) continue;

                    Undo.RecordObject(mb, UndoName);
                    ApplyToComponent(mb);
                    EditorUtility.SetDirty(mb);

                    if (PrefabUtility.IsPartOfPrefabInstance(mb))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(mb);
                    }

                    appliedCount++;
                }
            }

            if (applyAvatar)
            {
                foreach (var image in prefabRoot.GetComponentsInChildren<Image>(true))
                {
                    if (!IsStaticAvatarImage(image.gameObject, image.sprite)) continue;

                    Undo.RecordObject(image, UndoName);
                    image.sprite = targetAvatarSprite;
                    EditorUtility.SetDirty(image);

                    if (PrefabUtility.IsPartOfPrefabInstance(image))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                    }

                    appliedCount++;
                }
            }

            if (appliedCount > 0)
            {
                EditorUtility.SetDirty(prefabRoot);
            }
        }

        return appliedCount;
    }

    private bool HasSelectedSceneApplyTarget()
    {
        return characterUsages.Any(x => x.Selected && !x.Skipped) || sceneAvatarUsages.Any(x => x.Selected);
    }

    private bool CanApplyRuntimeWarning(RuntimeAssetWarning warning)
    {
        if (warning == null) return false;

        return (warning.HasSpineCandidates && HasSkeletonTargetInput()) ||
               (warning.HasStaticAvatarCandidates && targetAvatarSprite != null);
    }

    private bool HasSkeletonTargetInput()
    {
        return targetSkeletonDataAsset != null && !string.IsNullOrWhiteSpace(targetSkinName);
    }

    private bool ValidateRuntimeWarningInput(IEnumerable<RuntimeAssetWarning> warnings)
    {
        var warningList = warnings.Where(x => x != null).ToList();
        bool applySpine = warningList.Any(x => x.HasSpineCandidates) && HasSkeletonTargetInput();
        bool applyAvatar = warningList.Any(x => x.HasStaticAvatarCandidates) && targetAvatarSprite != null;

        if (!applySpine && !applyAvatar)
        {
            EditorUtility.DisplayDialog(
                "Scene Character Skin Applier",
                "Fill Skeleton Data Asset + Skin Name for Spine candidates, or drag Avatar Sprite for static Image avatars.",
                "OK");
            return false;
        }

        return !applySpine || ValidateTargetInput();
    }

    private bool ValidateAvatarTargetInput()
    {
        if (targetAvatarSprite == null)
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "Drag an Avatar Sprite first.", "OK");
            return false;
        }

        return true;
    }

    private string BuildApplyTargetSummary(bool applySpine, bool applyAvatar, bool hasStaticAvatarCandidates)
    {
        var lines = new List<string>();

        if (applySpine)
        {
            lines.Add($"Spine: {targetSkeletonDataAsset.name}/{targetSkinName}");
        }

        if (applyAvatar)
        {
            lines.Add($"Avatar sprite: {AssetDatabase.GetAssetPath(targetAvatarSprite)}");
        }
        else if (hasStaticAvatarCandidates)
        {
            lines.Add("Avatar sprite: <none> (static Image avatars will only be reported)");
        }

        return string.Join("\n", lines);
    }

    private bool ValidateTargetInput()
    {
        if (targetSkeletonDataAsset == null)
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "Drag a SkeletonDataAsset first.", "OK");
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetSkinName))
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", "Fill Skin Name first.", "OK");
            return false;
        }

        if (validateSkinName && !TargetSkinExists(out string validationError))
        {
            EditorUtility.DisplayDialog("Scene Character Skin Applier", validationError, "OK");
            return false;
        }

        return true;
    }

    private void ApplyToComponent(Component component)
    {
        if (component == null) return;

        SerializedObject so = new SerializedObject(component);
        SerializedProperty skelProp = so.FindProperty("skeletonDataAsset") ?? so.FindProperty("m_SkeletonDataAsset");
        SerializedProperty skinProp = so.FindProperty("initialSkinName") ?? so.FindProperty("_initialSkinName");
        if (skelProp != null) skelProp.objectReferenceValue = targetSkeletonDataAsset;
        if (skinProp != null) skinProp.stringValue = targetSkinName;
        so.ApplyModifiedProperties();

        TryInitializeReflection(component);
        if (component is Graphic graphic)
        {
            graphic.SetAllDirty();
        }
    }

    private bool TargetSkinExists(out string error)
    {
        error = "";

        try
        {
            MethodInfo getSkelData = targetSkeletonDataAsset.GetType().GetMethod("GetSkeletonData", new[] { typeof(bool) });
            if (getSkelData == null) return true;

            object skeletonData = getSkelData.Invoke(targetSkeletonDataAsset, new object[] { false });
            if (skeletonData == null)
            {
                error = $"SkeletonDataAsset '{targetSkeletonDataAsset.name}' has no skeleton data.";
                return false;
            }

            MethodInfo findSkin = skeletonData.GetType().GetMethod("FindSkin", new[] { typeof(string) });
            if (findSkin == null) return true;

            object skin = findSkin.Invoke(skeletonData, new object[] { targetSkinName });
            if (skin == null)
            {
                error = $"Skin '{targetSkinName}' not found in SkeletonDataAsset '{targetSkeletonDataAsset.name}'.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Cannot validate skin '{targetSkinName}' on '{targetSkeletonDataAsset.name}'.\n{ex.Message}";
            return false;
        }
    }

    private static void TryInitializeReflection(Component component)
    {
        try
        {
            MethodInfo init = component.GetType().GetMethod("Initialize", new[] { typeof(bool) });
            if (init != null)
            {
                init.Invoke(component, new object[] { true });
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SceneCharacterSkinApplier] Cannot initialize '{GetHierarchyPath(component.gameObject)}': {ex.Message}");
        }
    }

    private void ScanRuntimeAssetWarnings(Scene scene)
    {
        var seen = new HashSet<string>();

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;

                SerializedObject serializedObject;
                try
                {
                    serializedObject = new SerializedObject(behaviour);
                }
                catch
                {
                    continue;
                }

                var iterator = serializedObject.GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;

                    var referencedObject = iterator.objectReferenceValue;
                    if (referencedObject == null || !AssetDatabase.Contains(referencedObject)) continue;

                    CollectRuntimeWarningsFromReference(
                        GetHierarchyPath(behaviour.gameObject),
                        iterator.propertyPath,
                        null,
                        referencedObject,
                        runtimeWarnings,
                        seen,
                        0);
                }
            }
        }
    }

    private static void CollectRuntimeWarningsFromReference(
        string sceneObjectPath,
        string propertyPath,
        Object sourceAsset,
        Object referencedObject,
        List<RuntimeAssetWarning> warnings,
        HashSet<string> seen,
        int depth)
    {
        if (referencedObject == null || depth > 2) return;

        if (referencedObject is GameObject prefab)
        {
            AddPrefabRuntimeWarning(sceneObjectPath, propertyPath, sourceAsset, prefab, warnings, seen);
            return;
        }

        if (referencedObject is Component component)
        {
            AddPrefabRuntimeWarning(sceneObjectPath, propertyPath, sourceAsset, component.gameObject, warnings, seen);
            return;
        }

        if (referencedObject is ScriptableObject scriptableObject)
        {
            ScanScriptableObjectReferences(sceneObjectPath, propertyPath, scriptableObject, warnings, seen, depth + 1);
        }
    }

    private static void ScanScriptableObjectReferences(
        string sceneObjectPath,
        string parentPropertyPath,
        ScriptableObject scriptableObject,
        List<RuntimeAssetWarning> warnings,
        HashSet<string> seen,
        int depth)
    {
        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(scriptableObject);
        }
        catch
        {
            return;
        }

        var iterator = serializedObject.GetIterator();
        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;

            var referencedObject = iterator.objectReferenceValue;
            if (referencedObject == null || !AssetDatabase.Contains(referencedObject)) continue;

            string childPropertyPath = $"{parentPropertyPath} -> {iterator.propertyPath}";
            CollectRuntimeWarningsFromReference(
                sceneObjectPath,
                childPropertyPath,
                scriptableObject,
                referencedObject,
                warnings,
                seen,
                depth);
        }
    }

    private static void AddPrefabRuntimeWarning(
        string sceneObjectPath,
        string propertyPath,
        Object sourceAsset,
        GameObject prefab,
        List<RuntimeAssetWarning> warnings,
        HashSet<string> seen)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(prefabPath)) return;

        var prefabUsages = FindCharacterUsagesInPrefab(prefab);
        var staticAvatarUsages = FindStaticAvatarImageUsagesInPrefab(prefab);
        if (prefabUsages.Count == 0 && staticAvatarUsages.Count == 0) return;

        string sourceAssetPath = sourceAsset != null ? AssetDatabase.GetAssetPath(sourceAsset) : "";
        string key = $"{sceneObjectPath}|{propertyPath}|{sourceAssetPath}|{prefabPath}";
        if (!seen.Add(key)) return;

        warnings.Add(new RuntimeAssetWarning
        {
            SceneObjectPath = sceneObjectPath,
            PropertyPath = propertyPath,
            SourceAsset = sourceAsset,
            SourceAssetPath = sourceAssetPath,
            ReferencedAsset = prefab,
            ReferencedAssetPath = prefabPath,
            HasSpineCandidates = prefabUsages.Count > 0,
            HasStaticAvatarCandidates = staticAvatarUsages.Count > 0,
            Selected = false,
            Detail = BuildRuntimeWarningDetail(prefabUsages, staticAvatarUsages)
        });
    }

    private static string BuildRuntimeWarningDetail(List<CharacterUsage> prefabUsages, List<StaticAvatarImageUsage> staticAvatarUsages)
    {
        var lines = new List<string>();

        foreach (var usage in prefabUsages)
        {
            lines.Add($"Spine: {usage.ObjectPath} | {usage.ComponentType} | {usage.SkeletonAssetPath} | skin: {(string.IsNullOrEmpty(usage.InitialSkinName) ? "<empty>" : usage.InitialSkinName)}");
        }

        foreach (var usage in staticAvatarUsages)
        {
            lines.Add($"Static Image avatar: {usage.ObjectPath} | sprite: {usage.SpritePath}");
        }

        return string.Join("\n", lines);
    }

    private static List<CharacterUsage> FindCharacterUsagesInPrefab(GameObject prefab)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        if (!string.IsNullOrEmpty(prefabPath) && prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                return FindCharacterUsagesInRoot(prefabRoot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneCharacterSkinApplier] Cannot scan prefab '{prefabPath}': {ex.Message}");
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        return FindCharacterUsagesInRoot(prefab);
    }

    private static List<CharacterUsage> FindCharacterUsagesInRoot(GameObject root)
    {
        var result = new List<CharacterUsage>();
        if (root == null) return result;

        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (typeName == "SkeletonGraphic" || typeName == "SkeletonAnimation")
            {
                ReadSpineComponentData(mb, out Object skelAsset, out string initSkin);
                AddPrefabUsage(result, mb, skelAsset, initSkin, typeName);
            }
        }

        return result;
    }

    private static void AddPrefabUsage(
        List<CharacterUsage> result,
        Component component,
        Object skeletonDataAsset,
        string initialSkinName,
        string componentType)
    {
        string objectPath = GetHierarchyPath(component.gameObject);
        string skeletonPath = skeletonDataAsset != null ? AssetDatabase.GetAssetPath(skeletonDataAsset) : "";
        var classification = ClassifyCharacterUsage(objectPath, skeletonPath);
        if (!classification.IsCharacter) return;

        result.Add(new CharacterUsage
        {
            Component = component,
            GameObject = component.gameObject,
            ComponentType = componentType,
            ObjectPath = objectPath,
            SkeletonDataAsset = skeletonDataAsset,
            SkeletonAssetPath = string.IsNullOrEmpty(skeletonPath) ? "<missing>" : skeletonPath,
            InitialSkinName = initialSkinName,
            Selected = false,
            Skipped = false,
            Reason = classification.Reason
        });
    }

    private static List<StaticAvatarImageUsage> FindStaticAvatarImageUsagesInPrefab(GameObject prefab)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        if (!string.IsNullOrEmpty(prefabPath) && prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                return FindStaticAvatarImageUsagesInRoot(prefabRoot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneCharacterSkinApplier] Cannot scan static avatar Images in prefab '{prefabPath}': {ex.Message}");
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        return FindStaticAvatarImageUsagesInRoot(prefab);
    }

    private static List<StaticAvatarImageUsage> FindStaticAvatarImageUsagesInRoot(GameObject root)
    {
        var result = new List<StaticAvatarImageUsage>();
        if (root == null) return result;

        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (!IsStaticAvatarImage(image.gameObject, image.sprite)) continue;

            string spritePath = image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : "";
            result.Add(new StaticAvatarImageUsage
            {
                Image = image,
                GameObject = image.gameObject,
                Sprite = image.sprite,
                ObjectPath = GetHierarchyPath(image.gameObject),
                SpritePath = string.IsNullOrEmpty(spritePath) ? "<missing>" : spritePath,
                Selected = false,
                Reason = "Static UI avatar Image in runtime prefab."
            });
        }

        return result;
    }

    private static bool IsCharacterComponent(GameObject gameObject, Object skeletonDataAsset)
    {
        string objectPath = GetHierarchyPath(gameObject);
        string skeletonPath = skeletonDataAsset != null ? AssetDatabase.GetAssetPath(skeletonDataAsset) : "";
        return ClassifyCharacterUsage(objectPath, skeletonPath).IsCharacter;
    }

    private static bool IsStaticAvatarImage(GameObject gameObject, Sprite sprite)
    {
        if (gameObject == null || sprite == null) return false;

        string objectPath = Normalize(GetHierarchyPath(gameObject));
        string objectName = Normalize(gameObject.name);
        string spritePath = Normalize(AssetDatabase.GetAssetPath(sprite));
        string combined = $"{objectPath} {spritePath}";

        if (ContainsAny(
                combined,
                "locationicon",
                "progress_hotspring",
                "progress_forest",
                "circle_1",
                "circle_2",
                "icon_setting"))
        {
            return false;
        }

        if (ContainsAny(
                objectPath,
                "textbg",
                "textbubble",
                "texthint",
                "/text",
                "/bg",
                "/frame",
                "/mask",
                "/border",
                "/bar",
                "/fill",
                "playerava/image",
                "button",
                "chat box",
                "progress_bar"))
        {
            return false;
        }

        if (IsExactStaticAvatarObjectName(objectName))
        {
            return true;
        }

        return ContainsAny(
            objectPath,
            "travelmovingprogressbar/vehicle",
            "/avatar/",
            "/ava/");
    }

    private static bool IsExactStaticAvatarObjectName(string objectName)
    {
        switch (objectName)
        {
            case "avatar":
            case "ava":
            case "playeravatar":
            case "playerava":
            case "main_char":
            case "mainchar":
            case "characteravatar":
            case "customavatar":
            case "avatarimage":
                return true;
            default:
                return false;
        }
    }

    private static CharacterClassification ClassifyCharacterUsage(string objectPath, string skeletonAssetPath)
    {
        if (string.IsNullOrEmpty(skeletonAssetPath))
        {
            return CharacterClassification.Skip("Missing SkeletonDataAsset.");
        }

        string objectText = Normalize(objectPath);
        string assetText = Normalize(skeletonAssetPath);
        string combined = $"{objectText} {assetText}";

        if (ContainsAny(combined, "/hand", "handtut", "tutorial", "ticketnpc", "/npc", " npc", "_npc"))
        {
            return CharacterClassification.Skip("Skipped tutorial hand or NPC Spine.");
        }

        bool assetLooksCharacter =
            assetText.Contains("/animation/character/skin_character/") ||
            assetText.Contains("/animation/character/main_char/");

        bool objectLooksCharacter = ContainsAny(
            objectText,
            "main_char",
            "maincharacter",
            "mukbangcharacter",
            "characterpanel",
            "characterholder",
            "playerava",
            "avatar",
            "/player/",
            "player/");

        if (assetLooksCharacter && objectLooksCharacter)
        {
            return CharacterClassification.Accept("Character skeleton asset and character-like object path.");
        }

        if (assetLooksCharacter)
        {
            return CharacterClassification.Accept("Character skeleton asset.");
        }

        if (objectLooksCharacter)
        {
            return CharacterClassification.Accept("Character-like object path.");
        }

        return CharacterClassification.Skip("Spine object does not look like a character model.");
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (text.Contains(token)) return true;
        }

        return false;
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty).Replace("\\", "/").ToLowerInvariant();
    }

    private static string GetHierarchyPath(GameObject gameObject)
    {
        if (gameObject == null) return "<missing>";

        var names = new Stack<string>();
        Transform current = gameObject.transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static void PingObject(Object target)
    {
        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }

    private class CharacterUsage
    {
        public Component Component;
        public GameObject GameObject;
        public string ComponentType;
        public string ObjectPath;
        public Object SkeletonDataAsset;
        public string SkeletonAssetPath;
        public string InitialSkinName;
        public bool Selected;
        public bool Skipped;
        public string Reason;
    }

    private class RuntimeAssetWarning
    {
        public string SceneObjectPath;
        public string PropertyPath;
        public Object SourceAsset;
        public string SourceAssetPath;
        public Object ReferencedAsset;
        public string ReferencedAssetPath;
        public bool HasSpineCandidates;
        public bool HasStaticAvatarCandidates;
        public string Detail;
        public bool Selected;
    }

    private class StaticAvatarImageUsage
    {
        public Image Image;
        public GameObject GameObject;
        public Sprite Sprite;
        public string ObjectPath;
        public string SpritePath;
        public bool Selected;
        public string Reason;
    }

    private readonly struct CharacterClassification
    {
        public readonly bool IsCharacter;
        public readonly string Reason;

        private CharacterClassification(bool isCharacter, string reason)
        {
            IsCharacter = isCharacter;
            Reason = reason;
        }

        public static CharacterClassification Accept(string reason)
        {
            return new CharacterClassification(true, reason);
        }

        public static CharacterClassification Skip(string reason)
        {
            return new CharacterClassification(false, reason);
        }
    }
}
