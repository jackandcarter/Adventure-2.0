using System;
using System.Collections.Generic;
using Adventure.ScriptableObjects;
using UnityEngine;

namespace Adventure.Gameplay
{
    /// <summary>
    /// Tracks which abilities a player should have access to based on their class definition and level.
    /// </summary>
    public class AbilityLoadoutManager : MonoBehaviour
    {
        [SerializeField]
        private PlayerClassDefinition? activeClass;

        [SerializeField]
        private int currentLevel = 1;

        [SerializeField]
        private List<string> unlockedAbilities = new();

        public event Action<string, int>? AbilityUnlocked;

        public void SetClass(PlayerClassDefinition classDefinition)
        {
            activeClass = classDefinition;
            RebuildUnlocks();
        }

        public void SetLevel(int level)
        {
            currentLevel = Mathf.Max(1, level);
            RebuildUnlocks();
        }

        public IReadOnlyList<string> GetUnlockedAbilities() => unlockedAbilities;

        private void RebuildUnlocks()
        {
            unlockedAbilities.Clear();
            if (activeClass == null)
            {
                return;
            }

            foreach (var abilityId in activeClass.StartingAbilities)
            {
                RegisterUnlock(abilityId, -1);
            }

            foreach (var unlock in activeClass.AbilityUnlocks)
            {
                if (unlock.UnlockLevel <= currentLevel)
                {
                    RegisterUnlock(unlock.AbilityId, unlock.HotbarIndex);
                }
            }
        }

        private void RegisterUnlock(string abilityId, int hotbarIndex)
        {
            if (string.IsNullOrWhiteSpace(abilityId) || unlockedAbilities.Contains(abilityId))
            {
                return;
            }

            unlockedAbilities.Add(abilityId);
            AbilityUnlocked?.Invoke(abilityId, hotbarIndex);
        }
    }
}
