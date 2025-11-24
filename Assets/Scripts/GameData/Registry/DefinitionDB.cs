using System;
using System.Collections.Generic;
using Adventure.GameData.Definitions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Adventure.GameData.Registry
{
    public static class DefinitionDB
    {
        private const string RegistryResourcePath = "Registry/IDRegistry";
#if UNITY_EDITOR
        private const string RegistryAssetPath = "Assets/Resources/Registry/IDRegistry.asset";
#endif

        private static IDRegistry cachedRegistry;
        private static bool reportedMissingRegistry;

        private static IDRegistry Registry
        {
            get
            {
                if (cachedRegistry != null)
                {
                    return cachedRegistry;
                }

#if UNITY_EDITOR
                cachedRegistry = AssetDatabase.LoadAssetAtPath<IDRegistry>(RegistryAssetPath);
                if (cachedRegistry != null)
                {
                    return cachedRegistry;
                }
#endif
                cachedRegistry = Resources.Load<IDRegistry>(RegistryResourcePath);
                if (cachedRegistry == null && !reportedMissingRegistry)
                {
                    Debug.LogError($"Failed to load IDRegistry from resources path '{RegistryResourcePath}'. Ensure the registry asset exists.");
                    reportedMissingRegistry = true;
                }

                return cachedRegistry;
            }
        }

        public static ClassDefinition GetClass(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("DefinitionDB.GetClass was called with a null or empty id.");
                return null;
            }

            if (Registry == null)
            {
                return null;
            }

            return Registry.TryGetClass(id, out ClassDefinition definition) ? definition : null;
        }

        public static AbilityDefinition GetAbility(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("DefinitionDB.GetAbility was called with a null or empty id.");
                return null;
            }

            if (Registry == null)
            {
                return null;
            }

            return Registry.TryGetAbility(id, out AbilityDefinition definition) ? definition : null;
        }

        public static IReadOnlyList<ClassDefinition> GetAllClasses()
        {
            if (Registry == null || Registry.Classes == null)
            {
                return Array.Empty<ClassDefinition>();
            }

            List<ClassDefinition> results = new List<ClassDefinition>();
            foreach (IDRegistry.ClassEntry entry in Registry.Classes)
            {
                if (entry?.Asset != null && !results.Contains(entry.Asset))
                {
                    results.Add(entry.Asset);
                }
            }

            return results;
        }
    }
}
