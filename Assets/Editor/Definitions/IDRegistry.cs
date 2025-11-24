using System.Collections.Generic;
using Adventure.GameData.Definitions;
using UnityEditor;
using UnityEngine;

namespace Adventure.Editor.Definitions
{
    public static class IDRegistry
    {
        public static bool IsIdUnique<T>(string id, T current) where T : UnityEngine.Object, IIdentifiableDefinition
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return true;
            }

            string filter = $"t:{typeof(T).Name}";
            string[] guids = AssetDatabase.FindAssets(filter);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null && asset != current && asset.Id == id)
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
