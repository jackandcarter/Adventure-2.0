using Adventure.GameData.Definitions;
using UnityEditor;
using UnityEngine;

namespace Adventure.Editor.Definitions
{
    [CustomEditor(typeof(ClassDefinition))]
    public class ClassDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty idProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty iconProp;
        private SerializedProperty baseStatsProp;
        private SerializedProperty abilitiesProp;
        private SerializedProperty tagsProp;

        private void OnEnable()
        {
            idProp = serializedObject.FindProperty("id");
            displayNameProp = serializedObject.FindProperty("displayName");
            descriptionProp = serializedObject.FindProperty("description");
            iconProp = serializedObject.FindProperty("icon");
            baseStatsProp = serializedObject.FindProperty("baseStats");
            abilitiesProp = serializedObject.FindProperty("abilities");
            tagsProp = serializedObject.FindProperty("tags");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool hasErrors = DefinitionEditorUtility.DrawIdentitySection(idProp, displayNameProp, tagsProp, target);
            DefinitionEditorUtility.DrawVisualsSection(descriptionProp, iconProp);

            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(baseStatsProp, new GUIContent("Base Stats"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("Abilities", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(abilitiesProp, true);
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            if (hasErrors)
            {
                EditorGUILayout.HelpBox("Resolve validation errors to save this asset.", MessageType.Warning);
            }
        }
    }
}
