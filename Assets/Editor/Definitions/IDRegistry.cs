using System.Collections.Generic;
using Adventure.GameData.Definitions;
using UnityEditor;
using UnityEngine;

namespace Adventure.Editor.Definitions
{
    public static class IDRegistry
    {
        public static bool IsIdUnique(string id, UnityEngine.Object current)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return true;
            }

            if (current == null)
            {
                return true;
            }

            System.Type type = current.GetType();
            if (!typeof(IIdentifiableDefinition).IsAssignableFrom(type))
            {
                return true;
            }

            string filter = $"t:{type.Name}";
            string[] guids = AssetDatabase.FindAssets(filter);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, type);
                if (asset is IIdentifiableDefinition identifiable && asset != current && identifiable.Id == id)
                {
                    return false;
                }
            }

            return true;
        }

        public static IEnumerable<T> GetAllWithId<T>(string id) where T : UnityEngine.Object, IIdentifiableDefinition
        {
            string filter = $"t:{typeof(T).Name}";
            string[] guids = AssetDatabase.FindAssets(filter);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null && asset.Id == id)
                {
                    yield return asset;
                }
            }
        }
    }
}
