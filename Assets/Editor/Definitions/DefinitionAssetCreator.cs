using System;
using Adventure.GameData.Definitions;
using UnityEditor;
using UnityEngine;

namespace Adventure.Editor.Definitions
{
    public static class DefinitionAssetCreator
    {
        [MenuItem("Tools/Adventure/Create/Class Definition")]
        public static void CreateClassDefinition()
        {
            CreateAsset<ClassDefinition>("NewClassDefinition");
        }

        [MenuItem("Tools/Adventure/Create/Ability Definition")]
        public static void CreateAbilityDefinition()
        {
            CreateAsset<AbilityDefinition>("NewAbilityDefinition");
        }

        [MenuItem("Tools/Adventure/Create/Stat Block")]
        public static void CreateStatBlock()
        {
            CreateAsset<StatBlock>("NewStatBlock");
        }

        private static void CreateAsset<T>(string defaultName) where T : ScriptableObject, IIdentifiableDefinition
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Asset", defaultName, "asset", "Choose a location for the new asset");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            T asset = ScriptableObject.CreateInstance<T>();
            string generatedId = Guid.NewGuid().ToString("N");
            asset.SetIdentity(generatedId, typeof(T).Name);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
}
