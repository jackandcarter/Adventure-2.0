using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Adventure.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Adventure.EditorTools
{
    public static class DefinitionExportUtility
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public static string ExportRoot => Path.Combine(Application.dataPath, "../Server/Generated");

        public static void ExportSingle(IExportableDefinition definition)
        {
            var errors = new List<string>();
            definition.Validate(errors);
            if (errors.Count > 0)
            {
                Debug.LogError($"Cannot export {definition.DisplayName}:\n - {string.Join("\n - ", errors)}");
                return;
            }

            var payload = definition.ToExportModel();
            Directory.CreateDirectory(ExportRoot);
            var filePath = Path.Combine(ExportRoot, $"{definition.Id}.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(payload, Options));
            Debug.Log($"Exported {definition.DisplayName} to {filePath}");
        }

        public static void ExportAll<T>() where T : UnityEngine.Object, IExportableDefinition
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var exported = 0;
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                {
                    continue;
                }

                ExportSingle(asset);
                exported++;
            }

            Debug.Log($"Exported {exported} {typeof(T).Name} assets to {ExportRoot}.");
        }
    }

    public class DefinitionBatchExporter : EditorWindow
    {
        [MenuItem("Adventure/Definitions/Export All")]
        public static void ShowWindow()
        {
            GetWindow<DefinitionBatchExporter>("Definition Export");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Bulk export definitions to the server JSON directory.", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Export All Definitions"))
            {
                DefinitionExportUtility.ExportAll<AbilityDefinitionAsset>();
                DefinitionExportUtility.ExportAll<StatusEffectDefinitionAsset>();
                DefinitionExportUtility.ExportAll<ItemDefinition>();
                DefinitionExportUtility.ExportAll<PlayerClassDefinition>();
                DefinitionExportUtility.ExportAll<LootTableDefinition>();
                DefinitionExportUtility.ExportAll<EnemyArchetypeDefinition>();
                DefinitionExportUtility.ExportAll<RoomTemplateDefinition>();
                DefinitionExportUtility.ExportAll<DungeonThemeDefinition>();
            }
        }
    }

    public abstract class ValidatedDefinitionEditor<TDefinition> : UnityEditor.Editor
        where TDefinition : UnityEngine.Object, IExportableDefinition
    {
        private bool showValidation;
        private readonly List<string> lastErrors = new();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Validate"))
            {
                RunValidation();
            }

            if (GUILayout.Button("Export JSON"))
            {
                DefinitionExportUtility.ExportSingle((IExportableDefinition)target);
            }

            if (lastErrors.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", lastErrors), MessageType.Error);
            }
            else if (showValidation)
            {
                EditorGUILayout.HelpBox("No validation issues detected.", MessageType.Info);
            }
        }

        private void RunValidation()
        {
            lastErrors.Clear();
            showValidation = true;
            ((IExportableDefinition)target).Validate(lastErrors);
            if (lastErrors.Count == 0)
            {
                Debug.Log($"{target.name} passed validation.");
            }
        }
    }

    [CustomEditor(typeof(AbilityDefinitionAsset))]
    public class AbilityDefinitionEditor : ValidatedDefinitionEditor<AbilityDefinitionAsset> { }

    [CustomEditor(typeof(StatusEffectDefinitionAsset))]
    public class StatusEffectDefinitionEditor : ValidatedDefinitionEditor<StatusEffectDefinitionAsset> { }

    [CustomEditor(typeof(PlayerClassDefinition))]
    public class PlayerClassDefinitionEditor : ValidatedDefinitionEditor<PlayerClassDefinition> { }

    [CustomEditor(typeof(LootTableDefinition))]
    public class LootTableDefinitionEditor : ValidatedDefinitionEditor<LootTableDefinition> { }

    [CustomEditor(typeof(EnemyArchetypeDefinition))]
    public class EnemyArchetypeDefinitionEditor : ValidatedDefinitionEditor<EnemyArchetypeDefinition> { }

    [CustomEditor(typeof(RoomTemplateDefinition))]
    public class RoomTemplateDefinitionEditor : ValidatedDefinitionEditor<RoomTemplateDefinition>
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var definition = (RoomTemplateDefinition)target;
            var errors = new List<string>();
            if (definition.RoomPrefab != null)
            {
                RoomPrefabValidator.Validate(definition.RoomPrefab, errors);
            }

            if (errors.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
            }
        }
    }

    [CustomEditor(typeof(DungeonThemeDefinition))]
    public class DungeonThemeDefinitionEditor : ValidatedDefinitionEditor<DungeonThemeDefinition> { }
}
