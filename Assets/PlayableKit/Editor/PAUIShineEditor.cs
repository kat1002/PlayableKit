using UnityEditor;
using UnityEngine;

namespace PlayableKit.Editor   // NOT PlayableKit.Editor — would shadow UnityEditor.Editor
{
    /// <summary>Inspector for PAUIShine: greys out Loop Count when Loop is off and adds Play/Stop preview buttons (play mode only).</summary>
    [CustomEditor(typeof(PAUIShine)), CanEditMultipleObjects]
    public sealed class PAUIShineEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var loopProp = serializedObject.FindProperty("loop");
            var it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                bool disabled = it.propertyPath == "m_Script" ||
                    (it.propertyPath == "loopCount" && loopProp != null && !loopProp.boolValue && !loopProp.hasMultipleDifferentValues);
                using (new EditorGUI.DisabledScope(disabled))
                    EditorGUILayout.PropertyField(it, true);
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play Shine")) foreach (var t in targets) ((PAUIShine)t).PlayNow();
                if (GUILayout.Button("Stop")) foreach (var t in targets) ((PAUIShine)t).Stop();
            }
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode to preview the shine.", MessageType.None);
        }
    }
}
