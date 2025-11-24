using System;
using System.Collections.Generic;
using System.Reflection;
using Adventure.Editor.Registry;
using Adventure.Editor.Windows;
using Adventure.GameData.Registry;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Adventure.Tests.Editor
{
    public class GameDataRegistryTests
    {
        [Test]
        public void Registry_HasUniqueIdsAndResolvedReferences()
        {
            RegistryBuilder.BuildRegistry();

            IDRegistry registry = AssetDatabase.LoadAssetAtPath<IDRegistry>("Assets/Resources/Registry/IDRegistry.asset");
            Assert.IsNotNull(registry, "IDRegistry asset should exist after building.");

            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Assert.IsNotNull(registry.Classes, "Class entries should be initialized.");
            foreach (IDRegistry.ClassEntry entry in registry.Classes)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Id), "Class entry id should be set.");
                Assert.IsNotNull(entry.Asset, $"Class '{entry.Id}' should reference an asset.");
                Assert.AreEqual(entry.Id, entry.Asset.Id, "Class entry id must mirror the asset id.");
                Assert.IsTrue(seenIds.Add(entry.Id), $"Duplicate id detected for class '{entry.Id}'.");

                foreach (var ability in entry.Asset.Abilities)
                {
                    Assert.IsNotNull(ability, $"Class '{entry.Id}' should not contain null ability references.");
                }
            }

            Assert.IsNotNull(registry.Abilities, "Ability entries should be initialized.");
            foreach (IDRegistry.AbilityEntry entry in registry.Abilities)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Id), "Ability entry id should be set.");
                Assert.IsNotNull(entry.Asset, $"Ability '{entry.Id}' should reference an asset.");
                Assert.AreEqual(entry.Id, entry.Asset.Id, "Ability entry id must mirror the asset id.");
                Assert.IsTrue(seenIds.Add(entry.Id), $"Duplicate id detected for ability '{entry.Id}'.");
            }

            Assert.IsTrue(registry.Classes.Count > 0, "Registry should contain at least one class sample.");
            Assert.IsTrue(registry.Abilities.Count > 0, "Registry should contain at least one ability sample.");
        }

        [Test]
        public void DefinitionManagerWindow_RendersWithoutNullReferences()
        {
            DefinitionManagerWindow window = ScriptableObject.CreateInstance<DefinitionManagerWindow>();
            try
            {
                MethodInfo onEnable = typeof(DefinitionManagerWindow).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                onEnable?.Invoke(window, null);

                Event previous = Event.current;
                Event.current = new Event { type = EventType.Layout };

                MethodInfo onGui = typeof(DefinitionManagerWindow).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(onGui, "DefinitionManagerWindow.OnGUI should exist.");
                Assert.DoesNotThrow(() => onGui.Invoke(window, null));

                Event.current = previous;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
