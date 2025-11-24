using System;
using System.Collections.Generic;
using System.Reflection;
using Adventure.Editor.Registry;
using Adventure.GameData.Definitions;
using Adventure.GameData.Registry;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Adventure.Editor.Build
{
    public class RegistryBuildPreprocessor : IPreprocessBuildWithReport
    {
        private const string EditorRegistryAssetPath = "Assets/Resources/Registry/IDRegistry.asset";
        private const string RuntimeRegistryAssetPath = "Assets/Resources/Registry/FrozenIDRegistry.asset";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            RegistryBuilder.BuildRegistry();

            IDRegistry editorRegistry = AssetDatabase.LoadAssetAtPath<IDRegistry>(EditorRegistryAssetPath);
            if (editorRegistry == null)
            {
                throw new BuildFailedException($"Missing registry asset at '{EditorRegistryAssetPath}'. Run the Definition Manager or reimport definitions to regenerate it.");
            }

            FrozenDefinitionRegistry frozenRegistry = GetOrCreateFrozenRegistry();
            frozenRegistry.SetEntries(editorRegistry.Classes, editorRegistry.Abilities);
            EditorUtility.SetDirty(frozenRegistry);

            ValidateEntriesAreRuntimeSafe(editorRegistry.Classes, editorRegistry.Abilities);
            ValidateAddressables(editorRegistry, frozenRegistry);

            AssetDatabase.SaveAssets();
        }

        private static FrozenDefinitionRegistry GetOrCreateFrozenRegistry()
        {
            FrozenDefinitionRegistry registry = AssetDatabase.LoadAssetAtPath<FrozenDefinitionRegistry>(RuntimeRegistryAssetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<FrozenDefinitionRegistry>();
                AssetDatabase.CreateAsset(registry, RuntimeRegistryAssetPath);
            }

            return registry;
        }

        private static void ValidateEntriesAreRuntimeSafe(IEnumerable<IDRegistry.ClassEntry> classes, IEnumerable<IDRegistry.AbilityEntry> abilities)
        {
            foreach (IDRegistry.ClassEntry classEntry in classes)
            {
                EnsureRuntimeAsset(classEntry.Id, classEntry.Asset);
            }

            foreach (IDRegistry.AbilityEntry abilityEntry in abilities)
            {
                EnsureRuntimeAsset(abilityEntry.Id, abilityEntry.Asset);
            }
        }

        private static void EnsureRuntimeAsset(string id, UnityEngine.Object asset)
        {
            if (asset == null)
            {
                throw new BuildFailedException($"Registry entry '{id}' points to a missing asset.");
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (assetPath.Contains("/Editor/", StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException($"Registry entry '{id}' references '{assetPath}', which is inside an Editor-only folder.");
            }

            string[] labels = AssetDatabase.GetLabels(asset);
            foreach (string label in labels)
            {
                if (string.Equals(label, "EditorOnly", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BuildFailedException($"Registry entry '{id}' references '{assetPath}' which is labeled EditorOnly and will be stripped from the build.");
                }
            }
        }

        private static void ValidateAddressables(IDRegistry editorRegistry, FrozenDefinitionRegistry frozenRegistry)
        {
            object settings = TryGetAddressableSettings();
            if (settings == null)
            {
                return;
            }

            MethodInfo findEntryMethod = settings.GetType().GetMethod("FindAssetEntry", new[] { typeof(string) });
            if (findEntryMethod == null)
            {
                return;
            }

            List<string> missing = new List<string>();

            foreach (IDRegistry.ClassEntry classEntry in editorRegistry.Classes)
            {
                if (!IsAddressable(settings, findEntryMethod, classEntry.Asset))
                {
                    missing.Add($"Class '{classEntry.Id}' ({AssetDatabase.GetAssetPath(classEntry.Asset)})");
                }
            }

            foreach (IDRegistry.AbilityEntry abilityEntry in editorRegistry.Abilities)
            {
                if (!IsAddressable(settings, findEntryMethod, abilityEntry.Asset))
                {
                    missing.Add($"Ability '{abilityEntry.Id}' ({AssetDatabase.GetAssetPath(abilityEntry.Asset)})");
                }
            }

            if (!IsAddressable(settings, findEntryMethod, frozenRegistry))
            {
                missing.Add($"Runtime registry asset ({RuntimeRegistryAssetPath})");
            }

            if (missing.Count > 0)
            {
                throw new BuildFailedException($"Addressables configuration is missing entries for: {string.Join(", ", missing)}");
            }
        }

        private static object TryGetAddressableSettings()
        {
            Type settingsType = Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            if (settingsType == null)
            {
                return null;
            }

            PropertyInfo settingsProperty = settingsType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
            return settingsProperty?.GetValue(null, null);
        }

        private static bool IsAddressable(object settings, MethodInfo findEntryMethod, UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            object entry = findEntryMethod.Invoke(settings, new object[] { guid });
            return entry != null;
        }
    }
}
