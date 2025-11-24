using System;
using System.Collections.Generic;
using Adventure.GameData.Definitions;
using UnityEngine;

namespace Adventure.GameData.Registry
{
    [CreateAssetMenu(menuName = "Adventure/Registry/Frozen Definition Registry", fileName = "FrozenIDRegistry")]
    public class FrozenDefinitionRegistry : ScriptableObject
    {
        [Serializable]
        public struct ClassRecord
        {
            [SerializeField]
            private string id;

            [SerializeField]
            private ClassDefinition definition;

            public string Id => id;

            public ClassDefinition Definition => definition;

#if UNITY_EDITOR
            public ClassRecord(string id, ClassDefinition definition)
            {
                this.id = id;
                this.definition = definition;
            }
#endif
        }

        [Serializable]
        public struct AbilityRecord
        {
            [SerializeField]
            private string id;

            [SerializeField]
            private AbilityDefinition definition;

            public string Id => id;

            public AbilityDefinition Definition => definition;

#if UNITY_EDITOR
            public AbilityRecord(string id, AbilityDefinition definition)
            {
                this.id = id;
                this.definition = definition;
            }
#endif
        }

        [SerializeField]
        private List<ClassRecord> classes = new List<ClassRecord>();

        [SerializeField]
        private List<AbilityRecord> abilities = new List<AbilityRecord>();

        private Dictionary<string, ClassDefinition> classLookup;
        private Dictionary<string, AbilityDefinition> abilityLookup;

        public bool TryGetClass(string id, out ClassDefinition definition)
        {
            EnsureLookups();
            return classLookup.TryGetValue(id, out definition);
        }

        public bool TryGetAbility(string id, out AbilityDefinition definition)
        {
            EnsureLookups();
            return abilityLookup.TryGetValue(id, out definition);
        }

        public IReadOnlyList<ClassDefinition> GetAllClasses()
        {
            EnsureLookups();
            return new List<ClassDefinition>(classLookup.Values);
        }

        private void OnEnable()
        {
            classLookup = null;
            abilityLookup = null;
        }

        private void EnsureLookups()
        {
            if (classLookup != null && abilityLookup != null)
            {
                return;
            }

            classLookup = new Dictionary<string, ClassDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (ClassRecord record in classes)
            {
                if (string.IsNullOrWhiteSpace(record.Id) || record.Definition == null)
                {
                    continue;
                }

                if (!classLookup.ContainsKey(record.Id))
                {
                    classLookup.Add(record.Id, record.Definition);
                }
            }

            abilityLookup = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (AbilityRecord record in abilities)
            {
                if (string.IsNullOrWhiteSpace(record.Id) || record.Definition == null)
                {
                    continue;
                }

                if (!abilityLookup.ContainsKey(record.Id))
                {
                    abilityLookup.Add(record.Id, record.Definition);
                }
            }
        }

#if UNITY_EDITOR
        internal void SetEntries(IEnumerable<IDRegistry.ClassEntry> classEntries, IEnumerable<IDRegistry.AbilityEntry> abilityEntries)
        {
            classes.Clear();
            abilities.Clear();

            if (classEntries != null)
            {
                foreach (IDRegistry.ClassEntry entry in classEntries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Id) && entry.Asset != null)
                    {
                        classes.Add(new ClassRecord(entry.Id, entry.Asset));
                    }
                }
            }

            if (abilityEntries != null)
            {
                foreach (IDRegistry.AbilityEntry entry in abilityEntries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Id) && entry.Asset != null)
                    {
                        abilities.Add(new AbilityRecord(entry.Id, entry.Asset));
                    }
                }
            }

            classLookup = null;
            abilityLookup = null;
        }
#endif
    }
}
