using System;
using System.Collections.Generic;
using Adventure.GameData.Definitions;
using UnityEngine;

namespace Adventure.GameData.Registry
{
    [CreateAssetMenu(menuName = "Adventure/Registry/ID Registry", fileName = "IDRegistry")]
    public class IDRegistry : ScriptableObject
    {
        [Serializable]
        public class ClassEntry
        {
            [SerializeField]
            private string guid;

            [SerializeField]
            private string id;

            [SerializeField]
            private ClassDefinition asset;

            public string Guid => guid;

            public string Id => id;

            public ClassDefinition Asset => asset;

#if UNITY_EDITOR
            public ClassEntry(string guid, ClassDefinition asset)
            {
                this.guid = guid;
                this.asset = asset;
                id = asset != null ? asset.Id : string.Empty;
            }
#endif
        }

        [Serializable]
        public class AbilityEntry
        {
            [SerializeField]
            private string guid;

            [SerializeField]
            private string id;

            [SerializeField]
            private AbilityDefinition asset;

            public string Guid => guid;

            public string Id => id;

            public AbilityDefinition Asset => asset;

#if UNITY_EDITOR
            public AbilityEntry(string guid, AbilityDefinition asset)
            {
                this.guid = guid;
                this.asset = asset;
                id = asset != null ? asset.Id : string.Empty;
            }
#endif
        }

        [SerializeField]
        private List<ClassEntry> classes = new List<ClassEntry>();

        [SerializeField]
        private List<AbilityEntry> abilities = new List<AbilityEntry>();

        private Dictionary<string, ClassDefinition> classesById;
        private Dictionary<string, AbilityDefinition> abilitiesById;

        public IReadOnlyList<ClassEntry> Classes => classes;

        public IReadOnlyList<AbilityEntry> Abilities => abilities;

        public bool TryGetClass(string id, out ClassDefinition definition)
        {
            EnsureDictionaries();
            return classesById.TryGetValue(id, out definition);
        }

        public bool TryGetAbility(string id, out AbilityDefinition definition)
        {
            EnsureDictionaries();
            return abilitiesById.TryGetValue(id, out definition);
        }

        private void EnsureDictionaries()
        {
            if (classesById != null && abilitiesById != null)
            {
                return;
            }

            classesById = new Dictionary<string, ClassDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (ClassEntry entry in classes)
            {
                if (!string.IsNullOrWhiteSpace(entry.Id) && entry.Asset != null && !classesById.ContainsKey(entry.Id))
                {
                    classesById.Add(entry.Id, entry.Asset);
                }
            }

            abilitiesById = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (AbilityEntry entry in abilities)
            {
                if (!string.IsNullOrWhiteSpace(entry.Id) && entry.Asset != null && !abilitiesById.ContainsKey(entry.Id))
                {
                    abilitiesById.Add(entry.Id, entry.Asset);
                }
            }
        }

        private void OnEnable()
        {
            classesById = null;
            abilitiesById = null;
        }

#if UNITY_EDITOR
        internal void SetEntries(List<ClassEntry> newClasses, List<AbilityEntry> newAbilities)
        {
            classes = newClasses ?? new List<ClassEntry>();
            abilities = newAbilities ?? new List<AbilityEntry>();
            classesById = null;
            abilitiesById = null;
        }
#endif
    }
}
