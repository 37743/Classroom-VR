using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(OVRLipSyncContextMorphTarget))]
public class OVRLipSyncContextMorphTargetEditor : Editor
{
    SerializedProperty skinnedMeshRendererPrimary;
    SerializedProperty skinnedMeshRendererSecondary;
    SerializedProperty skinnedMeshRenderer; // For backward compatibility
    SerializedProperty visemeToBlendTargets;
    SerializedProperty visemeTestKeys;
    SerializedProperty laughterKey;
    SerializedProperty laughterBlendTarget;
    SerializedProperty laughterThreshold;
    SerializedProperty laughterMultiplier;
    SerializedProperty smoothAmount;
    private static string[] visemeNames = new string[] {
        "sil", "PP", "FF", "TH",
        "DD", "kk", "CH", "SS",
        "nn", "RR", "aa", "E",
        "ih", "oh", "ou"
    };

    void OnEnable()
    {
        skinnedMeshRendererPrimary = serializedObject.FindProperty("skinnedMeshRendererPrimary");
        skinnedMeshRendererSecondary = serializedObject.FindProperty("skinnedMeshRendererSecondary");
        skinnedMeshRenderer = serializedObject.FindProperty("skinnedMeshRenderer");
        visemeToBlendTargets = serializedObject.FindProperty("visemeToBlendTargets");
        visemeTestKeys = serializedObject.FindProperty("visemeTestKeys");
        laughterKey = serializedObject.FindProperty("laughterKey");
        laughterBlendTarget = serializedObject.FindProperty("laughterBlendTarget");
        laughterThreshold = serializedObject.FindProperty("laughterThreshold");
        laughterMultiplier = serializedObject.FindProperty("laughterMultiplier");
        smoothAmount = serializedObject.FindProperty("smoothAmount");
    }

    private void BlendNameProperty(SerializedProperty prop, string name, string[] blendNames = null)
    {
        if (blendNames == null)
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(name));
            return;
        }
        var values = new int[blendNames.Length + 1];
        var options = new GUIContent[blendNames.Length + 1];
        values[0] = -1;
        options[0] = new GUIContent("   ");
        for (int i = 0; i < blendNames.Length; ++i)
        {
            values[i + 1] = i;
            options[i + 1] = new GUIContent(blendNames[i]);
        }
        EditorGUILayout.IntPopup(prop, options, values, new GUIContent(name));
    }

    private string[] GetMeshBlendNames()
    {
        var morphTarget = (OVRLipSyncContextMorphTarget)serializedObject.targetObject;
        if (morphTarget == null || morphTarget.skinnedMeshRendererPrimary == null)
        {
            return null;
        }
        var mesh = morphTarget.skinnedMeshRendererPrimary.sharedMesh;
        var blendshapeCount = mesh.blendShapeCount;
        var blendNames = new string[blendshapeCount];
        for (int i = 0; i < mesh.blendShapeCount; ++i)
        {
            blendNames[i] = mesh.GetBlendShapeName(i);
        }
        return blendNames;
    }

    public override void OnInspectorGUI()
    {
        var blendNames = GetMeshBlendNames();
        var morphTarget = (OVRLipSyncContextMorphTarget)serializedObject.targetObject;

        serializedObject.Update();
        EditorGUILayout.PropertyField(skinnedMeshRendererPrimary, new GUIContent("Primary Skinned Mesh Renderer"));
        EditorGUILayout.PropertyField(skinnedMeshRendererSecondary, new GUIContent("Secondary Skinned Mesh Renderer"));
        if (EditorGUILayout.PropertyField(visemeToBlendTargets))
        {
            EditorGUI.indentLevel++;
            for (int i = 1; i < visemeNames.Length; ++i)
            {
                BlendNameProperty(visemeToBlendTargets.GetArrayElementAtIndex(i), visemeNames[i], blendNames);
            }
            BlendNameProperty(laughterBlendTarget, "Laughter", blendNames);
            EditorGUI.indentLevel--;
        }
        if (morphTarget)
        {
            morphTarget.enableVisemeTestKeys = EditorGUILayout.ToggleLeft("Enable Viseme Test Keys", morphTarget.enableVisemeTestKeys);
        }
        if (EditorGUILayout.PropertyField(visemeTestKeys))
        {
            EditorGUI.indentLevel++;
            for (int i = 1; i < visemeNames.Length; ++i)
            {
                EditorGUILayout.PropertyField(visemeTestKeys.GetArrayElementAtIndex(i), new GUIContent(visemeNames[i]));
            }
            EditorGUILayout.PropertyField(laughterKey, new GUIContent("Laughter"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.PropertyField(laughterThreshold);
        EditorGUILayout.PropertyField(laughterMultiplier);
        EditorGUILayout.PropertyField(smoothAmount);
        serializedObject.ApplyModifiedProperties();
    }
}