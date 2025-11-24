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
        private const string FrozenRegistryResourcePath = "Registry/FrozenIDRegistry";
        private const string EditorRegistryResourcePath = "Registry/IDRegistry";
#if UNITY_EDITOR
        private const string FrozenRegistryAssetPath = "Assets/Resources/Registry/FrozenIDRegistry.asset";
        private const string EditorRegistryAssetPath = "Assets/Resources/Registry/IDRegistry.asset";
#endif

        private static FrozenDefinitionRegistry cachedFrozenRegistry;
        private static IDRegistry cachedEditorRegistry;
        private static bool reportedMissingRegistry;

        private static FrozenDefinitionRegistry FrozenRegistry
        {
            get
            {
                if (cachedFrozenRegistry != null)
                {
                    return cachedFrozenRegistry;
                }

#if UNITY_EDITOR
                cachedFrozenRegistry = AssetDatabase.LoadAssetAtPath<FrozenDefinitionRegistry>(FrozenRegistryAssetPath);
                if (cachedFrozenRegistry != null)
                {
                    return cachedFrozenRegistry;
                }
#endif

                cachedFrozenRegistry = Resources.Load<FrozenDefinitionRegistry>(FrozenRegistryResourcePath);
                if (cachedFrozenRegistry == null && !reportedMissingRegistry)
                {
                    Debug.LogError($"Failed to load frozen registry from resources path '{FrozenRegistryResourcePath}'. Ensure the registry asset exists.");
                    reportedMissingRegistry = true;
                }

                return cachedFrozenRegistry;
            }
        }

#if UNITY_EDITOR
        private static IDRegistry EditorRegistry
        {
            get
            {
                if (cachedEditorRegistry != null)
                {
                    return cachedEditorRegistry;
                }

                cachedEditorRegistry = AssetDatabase.LoadAssetAtPath<IDRegistry>(EditorRegistryAssetPath);
                if (cachedEditorRegistry != null)
                {
                    return cachedEditorRegistry;
                }

                cachedEditorRegistry = Resources.Load<IDRegistry>(EditorRegistryResourcePath);
                return cachedEditorRegistry;
            }
        }
#endif

        public static ClassDefinition GetClass(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("DefinitionDB.GetClass was called with a null or empty id.");
                return null;
            }

            if (!TryGetClassDefinition(id, out ClassDefinition definition))
            {
                return null;
            }

            return definition;
        }

        public static AbilityDefinition GetAbility(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("DefinitionDB.GetAbility was called with a null or empty id.");
                return null;
            }

            if (!TryGetAbilityDefinition(id, out AbilityDefinition definition))
            {
                return null;
            }

            return definition;
        }

        public static StatDefinition GetStat(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("DefinitionDB.GetStat was called with a null or empty id.");
                return null;
            }

            if (!TryGetStatDefinition(id, out StatDefinition definition))
            {
                return null;
            }

            return definition;
        }

        public static IReadOnlyList<ClassDefinition> GetAllClasses()
        {
            FrozenDefinitionRegistry frozenRegistry = FrozenRegistry;
            if (frozenRegistry != null)
            {
                return frozenRegistry.GetAllClasses();
            }

#if UNITY_EDITOR
            if (EditorRegistry == null || EditorRegistry.Classes == null)
            {
                return Array.Empty<ClassDefinition>();
            }

            List<ClassDefinition> results = new List<ClassDefinition>();
            foreach (IDRegistry.ClassEntry entry in EditorRegistry.Classes)
            {
                if (entry?.Asset != null && !results.Contains(entry.Asset))
                {
                    results.Add(entry.Asset);
                }
            }

            return results;
#else
            return Array.Empty<ClassDefinition>();
#endif
        }

        public static IReadOnlyList<StatDefinition> GetAllStats()
        {
            FrozenDefinitionRegistry frozenRegistry = FrozenRegistry;
            if (frozenRegistry != null)
            {
                return frozenRegistry.GetAllStats();
            }

#if UNITY_EDITOR
            if (EditorRegistry == null || EditorRegistry.Stats == null)
            {
                return Array.Empty<StatDefinition>();
            }

            List<StatDefinition> results = new List<StatDefinition>();
            foreach (IDRegistry.StatEntry entry in EditorRegistry.Stats)
            {
                if (entry?.Asset != null && !results.Contains(entry.Asset))
                {
                    results.Add(entry.Asset);
                }
            }

            return results;
#else
            return Array.Empty<StatDefinition>();
#endif
        }

        private static bool TryGetClassDefinition(string id, out ClassDefinition definition)
        {
            FrozenDefinitionRegistry frozenRegistry = FrozenRegistry;
            if (frozenRegistry != null && frozenRegistry.TryGetClass(id, out definition))
            {
                return true;
            }

#if UNITY_EDITOR
            if (EditorRegistry != null && EditorRegistry.TryGetClass(id, out definition))
            {
                return true;
            }
#endif

            definition = null;
            return false;
        }

        private static bool TryGetAbilityDefinition(string id, out AbilityDefinition definition)
        {
            FrozenDefinitionRegistry frozenRegistry = FrozenRegistry;
            if (frozenRegistry != null && frozenRegistry.TryGetAbility(id, out definition))
            {
                return true;
            }

#if UNITY_EDITOR
            if (EditorRegistry != null && EditorRegistry.TryGetAbility(id, out definition))
            {
                return true;
            }
#endif

            definition = null;
            return false;
        }

        private static bool TryGetStatDefinition(string id, out StatDefinition definition)
        {
            FrozenDefinitionRegistry frozenRegistry = FrozenRegistry;
            if (frozenRegistry != null && frozenRegistry.TryGetStat(id, out definition))
            {
                return true;
            }

#if UNITY_EDITOR
            if (EditorRegistry != null && EditorRegistry.TryGetStat(id, out definition))
            {
                return true;
            }
#endif

            definition = null;
            return false;
        }
    }
}
