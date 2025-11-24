using System;
using System.Collections.Generic;
using Adventure.GameData.Definitions;
using Adventure.GameData.Registry;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Adventure.UI.ClassSelection
{
    public class ClassSelectionController : MonoBehaviour
    {
        [Serializable]
        public class ClassSelectedEvent : UnityEvent<ClassDefinition>
        {
        }

        [Header("List")]
        [SerializeField]
        private RectTransform listContainer;

        [SerializeField]
        private ClassSelectButton buttonPrefab;

        [Header("Details")]
        [SerializeField]
        private Image detailIcon;

        [SerializeField]
        private TMP_Text detailName;

        [SerializeField]
        private TMP_Text detailDescription;

        [SerializeField]
        private RectTransform statContainer;

        [SerializeField]
        private GameObject statEntryPrefab;

        [SerializeField]
        private RectTransform abilityContainer;

        [SerializeField]
        private GameObject abilityEntryPrefab;

        [Header("Selection")]
        [SerializeField]
        private Button selectButton;

        [SerializeField]
        private TMP_Text selectButtonLabel;

        [SerializeField]
        private MonoBehaviour playerStateTarget;

        [SerializeField]
        private ClassSelectedEvent onClassSelected = new ClassSelectedEvent();

        private readonly List<ClassSelectButton> spawnedButtons = new List<ClassSelectButton>();
        private IPlayerClassSetter playerClassSetter;
        private ClassDefinition currentSelection;

        private void Awake()
        {
            playerClassSetter = playerStateTarget as IPlayerClassSetter;

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(HandleSelectionConfirmed);
                selectButton.interactable = false;
            }
        }

        private void OnEnable()
        {
            RefreshList();
        }

        private void OnDisable()
        {
            ClearButtons();
        }

        public void RefreshList()
        {
            ClearButtons();

            IReadOnlyList<ClassDefinition> classes = DefinitionDB.GetAllClasses();
            if (classes == null || classes.Count == 0 || listContainer == null || buttonPrefab == null)
            {
                return;
            }

            foreach (ClassDefinition definition in classes)
            {
                if (definition == null)
                {
                    continue;
                }

                ClassSelectButton button = Instantiate(buttonPrefab, listContainer);
                button.SetClass(definition);
                button.OnClicked.AddListener(HandleClassSelected);
                spawnedButtons.Add(button);
            }

            if (spawnedButtons.Count > 0)
            {
                HandleClassSelected(spawnedButtons[0].ClassDefinition);
            }
        }

        private void HandleClassSelected(ClassDefinition definition)
        {
            currentSelection = definition;
            UpdateDetails(definition);

            if (selectButton != null)
            {
                selectButton.interactable = definition != null;
            }
        }

        private void HandleSelectionConfirmed()
        {
            if (currentSelection == null)
            {
                return;
            }

            if (playerClassSetter == null)
            {
                Debug.LogError("ClassSelectionController does not have a valid IPlayerClassSetter target to apply the selected class.");
                return;
            }

            playerClassSetter.SetClass(currentSelection);
            onClassSelected?.Invoke(currentSelection);
        }

        private void UpdateDetails(ClassDefinition definition)
        {
            if (detailIcon != null)
            {
                detailIcon.sprite = definition != null ? definition.Icon : null;
                detailIcon.enabled = detailIcon.sprite != null;
            }

            if (detailName != null)
            {
                detailName.text = definition != null ? definition.DisplayName : string.Empty;
            }

            if (detailDescription != null)
            {
                detailDescription.text = definition != null ? definition.Description : string.Empty;
            }

            if (selectButtonLabel != null)
            {
                selectButtonLabel.text = definition != null ? $"Select {definition.DisplayName}" : "Select";
            }

            PopulateStats(definition?.BaseStats);
            PopulateAbilities(definition?.Abilities);
        }

        private void PopulateStats(StatBlock statBlock)
        {
            ClearContainer(statContainer);

            if (statContainer == null || statEntryPrefab == null || statBlock == null)
            {
                return;
            }

            foreach (StatGrowth stat in statBlock.Stats)
            {
                GameObject entry = Instantiate(statEntryPrefab, statContainer);
                TMP_Text[] labels = entry.GetComponentsInChildren<TMP_Text>();
                if (labels.Length > 0)
                {
                    string statName = DefinitionDB.GetStat(stat.StatId)?.DisplayName;
                    labels[0].text = string.IsNullOrWhiteSpace(statName) ? stat.StatId : statName;
                }

                if (labels.Length > 1)
                {
                    labels[1].text = stat.Evaluate(1).ToString();
                }
            }
        }

        private void PopulateAbilities(IReadOnlyList<AbilityDefinition> abilities)
        {
            ClearContainer(abilityContainer);

            if (abilityContainer == null || abilityEntryPrefab == null || abilities == null)
            {
                return;
            }

            foreach (AbilityDefinition ability in abilities)
            {
                if (ability == null)
                {
                    continue;
                }

                GameObject entry = Instantiate(abilityEntryPrefab, abilityContainer);
                TMP_Text[] labels = entry.GetComponentsInChildren<TMP_Text>();
                if (labels.Length > 0)
                {
                    labels[0].text = ability.DisplayName;
                }

                if (labels.Length > 1)
                {
                    labels[1].text = string.IsNullOrWhiteSpace(ability.EffectSummary) ? ability.Description : ability.EffectSummary;
                }
            }
        }

        private void ClearButtons()
        {
            foreach (ClassSelectButton button in spawnedButtons)
            {
                if (button != null)
                {
                    button.OnClicked.RemoveListener(HandleClassSelected);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(button.gameObject);
                        continue;
                    }
#endif
                    Destroy(button.gameObject);
                }
            }

            spawnedButtons.Clear();
        }

        private static void ClearContainer(Transform container)
        {
            if (container == null)
            {
                return;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child.gameObject);
                    continue;
                }
#endif
                Destroy(child.gameObject);
            }
        }
    }
}
