using System;
using System.Collections.Generic;
using Adventure.GameData.Definitions;
using Adventure.GameData.Registry;
using Adventure.UI.ClassSelection;
using UnityEngine;
using UnityEngine.Events;

namespace Adventure.Gameplay.Player
{
    public class PlayerClassState : MonoBehaviour, IPlayerClassSetter
    {
        [Serializable]
        public class ClassChangedEvent : UnityEvent<ClassDefinition>
        {
        }

        [Serializable]
        public class StatsAppliedEvent : UnityEvent<IReadOnlyDictionary<string, float>>
        {
        }

        [Header("Defaults")]
        [SerializeField]
        private ClassDefinition startingClass;

        [SerializeField]
        private string startingClassId;

        [Header("Events")]
        [SerializeField]
        private ClassChangedEvent onClassChanged = new ClassChangedEvent();

        [SerializeField]
        private StatsAppliedEvent onBaseStatsApplied = new StatsAppliedEvent();

        private readonly Dictionary<string, float> baseStatValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private ClassDefinition currentClass;

        public ClassDefinition CurrentClass => currentClass;
        public IReadOnlyDictionary<string, float> BaseStatValues => baseStatValues;

        public event Action<ClassDefinition> ClassChanged;
        public event Action<IReadOnlyDictionary<string, float>> BaseStatsApplied;

        private void Awake()
        {
            if (currentClass == null)
            {
                ClassDefinition loaded = LoadStartingClass();
                if (loaded != null)
                {
                    startingClass = loaded;
                }
            }

            if (startingClass != null)
            {
                SetClass(startingClass);
            }
            else if (!string.IsNullOrWhiteSpace(startingClassId))
            {
                Debug.LogWarning($"PlayerClassState has a starting class id '{startingClassId}' but the class could not be found in the registry.");
            }
        }

        public void SetClass(ClassDefinition classDefinition)
        {
            if (classDefinition == null)
            {
                Debug.LogError("PlayerClassState cannot set a null class definition.");
                return;
            }

            currentClass = classDefinition;
            ApplyBaseStats(classDefinition.BaseStats);

            onClassChanged?.Invoke(currentClass);
            ClassChanged?.Invoke(currentClass);
        }

        public void SetClassById(string classId)
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                Debug.LogError("PlayerClassState.SetClassById was called with a null or empty id.");
                return;
            }

            ClassDefinition definition = DefinitionDB.GetClass(classId);
            if (definition == null)
            {
                Debug.LogError($"PlayerClassState could not find ClassDefinition with id '{classId}' in the registry.");
                return;
            }

            SetClass(definition);
        }

        public float GetStatValue(string statId)
        {
            if (string.IsNullOrWhiteSpace(statId))
            {
                return 0f;
            }

            return baseStatValues.TryGetValue(statId, out float value) ? value : 0f;
        }

        private void ApplyBaseStats(StatBlock statBlock)
        {
            baseStatValues.Clear();

            if (statBlock != null)
            {
                foreach (StatGrowth stat in statBlock.Stats)
                {
                    if (stat == null || string.IsNullOrWhiteSpace(stat.StatId))
                    {
                        continue;
                    }

                    baseStatValues[stat.StatId] = stat.Evaluate(1);
                }
            }

            onBaseStatsApplied?.Invoke(baseStatValues);
            BaseStatsApplied?.Invoke(baseStatValues);
        }

        private ClassDefinition LoadStartingClass()
        {
            if (startingClass != null)
            {
                return startingClass;
            }

            if (string.IsNullOrWhiteSpace(startingClassId))
            {
                return null;
            }

            return DefinitionDB.GetClass(startingClassId);
        }
    }
}
