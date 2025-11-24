using Adventure.GameData.Definitions;
using UnityEditor;
using UnityEngine;

namespace Adventure.Editor.Definitions
{
    public static class DefinitionEditorUtility
    {
        public static bool DrawIdentitySection(SerializedProperty idProp, SerializedProperty displayNameProp, SerializedProperty tagsProp, UnityEngine.Object target)
        {
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(idProp);
            EditorGUILayout.PropertyField(displayNameProp);
            bool missing = false;

            if (string.IsNullOrWhiteSpace(idProp.stringValue))
            {
                EditorGUILayout.HelpBox("ID is required.", MessageType.Error);
                missing = true;
            }

            if (string.IsNullOrWhiteSpace(displayNameProp.stringValue))
            {
                EditorGUILayout.HelpBox("Display Name is required.", MessageType.Error);
                missing = true;
            }

            if (target is IIdentifiableDefinition identifiable && !IDRegistry.IsIdUnique(idProp.stringValue, (UnityEngine.Object)target))
            {
                string message = $"ID '{idProp.stringValue}' is already used.";
                EditorGUILayout.HelpBox(message, MessageType.Error);
                missing = true;
            }

            if (tagsProp != null)
            {
                EditorGUILayout.PropertyField(tagsProp, true);
            }

            EditorGUILayout.EndVertical();
            return missing;
        }

        public static void DrawVisualsSection(SerializedProperty descriptionProp, SerializedProperty iconProp)
        {
            EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            if (descriptionProp != null)
            {
                EditorGUILayout.PropertyField(descriptionProp);
            }

            if (iconProp != null)
            {
                EditorGUILayout.PropertyField(iconProp);
            }

            EditorGUILayout.EndVertical();
        }

        public static void DrawStatGrowth(SerializedProperty statProperty, ref int previewLevel)
        {
            SerializedProperty statIdProp = statProperty.FindPropertyRelative("statId");
            SerializedProperty baseValueProp = statProperty.FindPropertyRelative("baseValue");
            SerializedProperty growthCurveProp = statProperty.FindPropertyRelative("growthCurve");
            SerializedProperty useFormulaProp = statProperty.FindPropertyRelative("useFormula");
            SerializedProperty formulaProp = statProperty.FindPropertyRelative("formula");

            EditorGUILayout.PropertyField(statIdProp);
            EditorGUILayout.PropertyField(baseValueProp);

            EditorGUILayout.PropertyField(useFormulaProp, new GUIContent("Use Formula"));
            if (useFormulaProp.boolValue)
            {
                EditorGUILayout.PropertyField(formulaProp);
            }
            else
            {
                EditorGUILayout.PropertyField(growthCurveProp);
            }

            DrawStatPreview(baseValueProp.floatValue, growthCurveProp.animationCurveValue, useFormulaProp.boolValue, formulaProp.stringValue, ref previewLevel);
        }

        public static void DrawStatPreview(float baseValue, AnimationCurve curve, bool useFormula, string formula, ref int previewLevel)
        {
            EditorGUILayout.BeginHorizontal();
            previewLevel = EditorGUILayout.IntSlider("Preview Level", previewLevel, 1, 100);
            float evaluated = DefinitionMath.EvaluateCurveOrFormula(baseValue, curve, useFormula, formula, previewLevel);
            EditorGUILayout.LabelField($"Value: {evaluated:F2}");
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawAbilityEffectPreview(SerializedProperty baseEffectProp, SerializedProperty curveProp, SerializedProperty useFormulaProp, SerializedProperty formulaProp, ref int previewLevel)
        {
            EditorGUILayout.PropertyField(baseEffectProp, new GUIContent("Base Effect"));
            EditorGUILayout.PropertyField(useFormulaProp, new GUIContent("Use Effect Formula"));
            if (useFormulaProp.boolValue)
            {
                EditorGUILayout.PropertyField(formulaProp);
            }
            else
            {
                EditorGUILayout.PropertyField(curveProp, new GUIContent("Effect Growth"));
            }

            DrawStatPreview(baseEffectProp.floatValue, curveProp.animationCurveValue, useFormulaProp.boolValue, formulaProp.stringValue, ref previewLevel);
        }
    }
}
