using System.Collections.Generic;
using Adventure.GameData.Definitions;
using Adventure.Gameplay.Player;
using UnityEngine;

namespace Adventure.UI.Hotbar
{
    public class HotbarController : MonoBehaviour
    {
        [SerializeField]
        private PlayerClassState playerClassState;

        [SerializeField]
        private RectTransform slotContainer;

        [SerializeField]
        private HotbarAbilitySlot slotPrefab;

        private readonly List<HotbarAbilitySlot> spawnedSlots = new List<HotbarAbilitySlot>();

        private void OnEnable()
        {
            if (playerClassState != null)
            {
                playerClassState.ClassChanged += HandleClassChanged;
            }

            if (playerClassState?.CurrentClass != null)
            {
                PopulateSlots(playerClassState.CurrentClass);
            }
        }

        private void OnDisable()
        {
            if (playerClassState != null)
            {
                playerClassState.ClassChanged -= HandleClassChanged;
            }

            ClearSlots();
        }

        private void HandleClassChanged(ClassDefinition newClass)
        {
            PopulateSlots(newClass);
        }

        private void PopulateSlots(ClassDefinition classDefinition)
        {
            ClearSlots();

            if (classDefinition == null)
            {
                Debug.LogError("HotbarController received a null ClassDefinition.");
                return;
            }

            if (slotContainer == null || slotPrefab == null)
            {
                Debug.LogError("HotbarController is missing slotContainer or slotPrefab references.");
                return;
            }

            IReadOnlyList<AbilityDefinition> abilities = classDefinition.Abilities;
            if (abilities == null || abilities.Count == 0)
            {
                Debug.LogWarning($"Class '{classDefinition.DisplayName}' does not define any abilities for the hotbar.");
                return;
            }

            foreach (AbilityDefinition ability in abilities)
            {
                if (ability == null)
                {
                    Debug.LogError($"Class '{classDefinition.DisplayName}' has a missing ability reference in its abilities list.");
                    continue;
                }

                HotbarAbilitySlot slot = Instantiate(slotPrefab, slotContainer);
                slot.SetAbility(ability);
                spawnedSlots.Add(slot);
            }
        }

        private void ClearSlots()
        {
            foreach (HotbarAbilitySlot slot in spawnedSlots)
            {
                if (slot == null)
                {
                    continue;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(slot.gameObject);
                    continue;
                }
#endif
                Destroy(slot.gameObject);
            }

            spawnedSlots.Clear();
        }
    }
}
