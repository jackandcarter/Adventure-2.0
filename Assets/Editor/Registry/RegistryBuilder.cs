using System;
using System.Collections.Generic;
using System.IO;
using Adventure.GameData.Definitions;
using Adventure.GameData.Registry;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Adventure.Editor.Registry
{
    public class RegistryBuilder : AssetPostprocessor
    {
        private const string RegistryDirectory = "Assets/Resources/Registry";
        private const string RegistryAssetPath = RegistryDirectory + "/IDRegistry.asset";
        private const string FrozenRegistryAssetPath = RegistryDirectory + "/FrozenIDRegistry.asset";
        internal const string RegistryResourcePath = "Registry/IDRegistry";

        private static bool isBuilding;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += BuildRegistry;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (isBuilding)
            {
                return;
            }

            if (ContainsRelevantChange(importedAssets) || ContainsRelevantChange(deletedAssets) || ContainsRelevantChange(movedAssets) || ContainsRelevantChange(movedFromAssetPaths))
            {
                BuildRegistry();
            }
        }

        private static bool ContainsRelevantChange(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && !IsRegistryAsset(path))
                {
                    return true;
                }
            }

            return false;
        }

        private static IDRegistry GetOrCreateRegistry()
        {
            EnsureDirectory();
            IDRegistry registry = AssetDatabase.LoadAssetAtPath<IDRegistry>(RegistryAssetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<IDRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            }

            return registry;
        }

        private static void EnsureDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(RegistryDirectory))
            {
                string parent = Path.GetDirectoryName(RegistryDirectory);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(RegistryDirectory))
                {
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(RegistryDirectory));
                }
            }
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            BuildRegistry();
        }

        internal static void BuildRegistry()
        {
            if (isBuilding)
            {
                return;
            }

            isBuilding = true;
            try
            {
                IDRegistry registry = GetOrCreateRegistry();
                List<IDRegistry.ClassEntry> classEntries = CollectEntries<ClassDefinition, IDRegistry.ClassEntry>((guid, asset) => new IDRegistry.ClassEntry(guid, asset));
                List<IDRegistry.AbilityEntry> abilityEntries = CollectEntries<AbilityDefinition, IDRegistry.AbilityEntry>((guid, asset) => new IDRegistry.AbilityEntry(guid, asset));
                List<IDRegistry.StatEntry> statEntries = CollectEntries<StatDefinition, IDRegistry.StatEntry>((guid, asset) => new IDRegistry.StatEntry(guid, asset));

                ValidateEntries(classEntries, abilityEntries, statEntries);

                registry.SetEntries(classEntries, abilityEntries, statEntries);
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                isBuilding = false;
            }
        }

        private static bool IsRegistryAsset(string path)
        {
            return path.Equals(RegistryAssetPath, StringComparison.OrdinalIgnoreCase)
                   || path.Equals(FrozenRegistryAssetPath, StringComparison.OrdinalIgnoreCase);
        }

        private static List<TEntry> CollectEntries<TDefinition, TEntry>(Func<string, TDefinition, TEntry> factory)
            where TDefinition : UnityEngine.Object, IIdentifiableDefinition
        {
            var entries = new List<TEntry>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(TDefinition).Name}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TDefinition asset = AssetDatabase.LoadAssetAtPath<TDefinition>(path);
                if (asset == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(asset.Id))
                {
                    Debug.LogWarning($"{typeof(TDefinition).Name} at '{path}' is missing an ID and will be ignored in the registry.");
                    continue;
                }

                entries.Add(factory(guid, asset));
            }

            return entries;
        }

        private static void ValidateEntries(IReadOnlyList<IDRegistry.ClassEntry> classEntries, IReadOnlyList<IDRegistry.AbilityEntry> abilityEntries, IReadOnlyList<IDRegistry.StatEntry> statEntries)
        {
            var idUsage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ValidateGroup(classEntries, "Class", idUsage, entry => entry.Id, entry => entry.Guid);
            ValidateGroup(abilityEntries, "Ability", idUsage, entry => entry.Id, entry => entry.Guid);
            ValidateGroup(statEntries, "Stat", idUsage, entry => entry.Id, entry => entry.Guid);
        }

        private static void ValidateGroup<TEntry>(IEnumerable<TEntry> entries, string label, Dictionary<string, string> idUsage, Func<TEntry, string> idSelector, Func<TEntry, string> guidSelector)
        {
            foreach (TEntry entry in entries)
            {
                string id = idSelector(entry);
                string guid = guidSelector(entry);
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogWarning($"{label} definition at '{path}' is missing an ID.");
                    continue;
                }

                if (!idUsage.TryAdd(id, $"{label} at {path}"))
                {
                    Debug.LogWarning($"Duplicate ID '{id}' found on {label} at '{path}' (previously used by {idUsage[id]}).");
                }

                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null)
                {
                    Debug.LogWarning($"{label} definition at '{path}' could not be loaded and may be missing from the registry.");
                }
            }
        }

        private class RegistrySaveProcessor : AssetModificationProcessor
        {
            public static string[] OnWillSaveAssets(string[] paths)
            {
                BuildRegistry();
                return paths;
            }
        }
    }
}
