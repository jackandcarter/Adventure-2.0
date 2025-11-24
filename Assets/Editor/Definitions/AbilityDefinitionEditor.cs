using Adventure.GameData.Definitions;
using UnityEditor;
using UnityEngine;

namespace Adventure.Editor.Definitions
{
    [CustomEditor(typeof(AbilityDefinition))]
    public class AbilityDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty idProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty iconProp;
        private SerializedProperty cooldownProp;
        private SerializedProperty costsProp;
        private SerializedProperty tagsProp;
        private SerializedProperty baseEffectProp;
        private SerializedProperty curveProp;
        private SerializedProperty useFormulaProp;
        private SerializedProperty formulaProp;
        private SerializedProperty effectSummaryProp;

        private int previewLevel = 1;

        private void OnEnable()
        {
            idProp = serializedObject.FindProperty("id");
            displayNameProp = serializedObject.FindProperty("displayName");
            descriptionProp = serializedObject.FindProperty("description");
            iconProp = serializedObject.FindProperty("icon");
            cooldownProp = serializedObject.FindProperty("cooldownSeconds");
            costsProp = serializedObject.FindProperty("resourceCosts");
            tagsProp = serializedObject.FindProperty("tags");
            baseEffectProp = serializedObject.FindProperty("baseEffectValue");
            curveProp = serializedObject.FindProperty("effectGrowthCurve");
            useFormulaProp = serializedObject.FindProperty("useEffectFormula");
            formulaProp = serializedObject.FindProperty("effectFormula");
            effectSummaryProp = serializedObject.FindProperty("effectSummary");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool hasErrors = DefinitionEditorUtility.DrawIdentitySection(idProp, displayNameProp, tagsProp, target);
            DefinitionEditorUtility.DrawVisualsSection(descriptionProp, iconProp);

            EditorGUILayout.LabelField("Cooldown & Costs", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(cooldownProp, new GUIContent("Cooldown (seconds)"));
            EditorGUILayout.PropertyField(costsProp, new GUIContent("Resource Costs"), true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("Abilities", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            DefinitionEditorUtility.DrawAbilityEffectPreview(baseEffectProp, curveProp, useFormulaProp, formulaProp, ref previewLevel);
            EditorGUILayout.PropertyField(effectSummaryProp, new GUIContent("Effect Summary"));
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            if (hasErrors)
            {
                EditorGUILayout.HelpBox("Resolve validation errors to save this asset.", MessageType.Warning);
            }
        }
    }
}
