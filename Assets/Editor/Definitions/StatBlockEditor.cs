using Adventure.GameData.Definitions;
using UnityEditor;
using UnityEngine;

namespace Adventure.Editor.Definitions
{
    [CustomEditor(typeof(StatBlock))]
    public class StatBlockEditor : UnityEditor.Editor
    {
        private SerializedProperty idProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty iconProp;
        private SerializedProperty statsProp;

        private int previewLevel = 1;

        private void OnEnable()
        {
            idProp = serializedObject.FindProperty("id");
            displayNameProp = serializedObject.FindProperty("displayName");
            descriptionProp = serializedObject.FindProperty("description");
            iconProp = serializedObject.FindProperty("icon");
            statsProp = serializedObject.FindProperty("stats");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool hasErrors = DefinitionEditorUtility.DrawIdentitySection(idProp, displayNameProp, null, target);
            DefinitionEditorUtility.DrawVisualsSection(descriptionProp, iconProp);

            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(statsProp, new GUIContent("Stats"), false);
            if (statsProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                statsProp.arraySize = EditorGUILayout.IntField("Count", statsProp.arraySize);
                for (int i = 0; i < statsProp.arraySize; i++)
                {
                    SerializedProperty statProperty = statsProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginVertical("box");
                    DefinitionEditorUtility.DrawStatGrowth(statProperty, ref previewLevel);
                    if (GUILayout.Button("Remove Stat"))
                    {
                        statsProp.DeleteArrayElementAtIndex(i);
                    }

                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("Add Stat"))
                {
                    statsProp.InsertArrayElementAtIndex(statsProp.arraySize);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            if (hasErrors)
            {
                EditorGUILayout.HelpBox("Resolve validation errors to save this asset.", MessageType.Warning);
            }
        }
    }
}
