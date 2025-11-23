using System.Text;
using Adventure.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure.UI.Dungeon
{
    /// <summary>
    /// Connects dungeon HUD widgets to GameStateClient updates without instantiating runtime UI.
    /// </summary>
    public class DungeonHudController : MonoBehaviour
    {
        [SerializeField]
        private GameStateClient gameStateClient;

        [Header("Vitals")]
        [SerializeField]
        private Slider healthBar;

        [SerializeField]
        private Slider manaBar;

        [SerializeField]
        private Slider experienceBar;

        [SerializeField]
        private Text levelLabel;

        [Header("Party")]
        [SerializeField]
        private Text partyList;

        [Header("Abilities")]
        [SerializeField]
        private Image[] abilityIcons;

        [SerializeField]
        private Image[] cooldownMasks;

        [Header("Interaction")]
        [SerializeField]
        private GameObject promptRoot;

        [SerializeField]
        private Text promptText;

        private void Awake()
        {
            if (gameStateClient == null)
            {
                gameStateClient = FindObjectOfType<GameStateClient>();
            }

            if (gameStateClient != null)
            {
                gameStateClient.PlayerVitalsUpdated += OnVitalsUpdated;
                gameStateClient.PartyUpdated += OnPartyUpdated;
                gameStateClient.AbilityBarUpdated += OnAbilityBarUpdated;
                gameStateClient.InteractionPromptUpdated += OnPromptUpdated;
            }
        }

        private void OnDestroy()
        {
            if (gameStateClient != null)
            {
                gameStateClient.PlayerVitalsUpdated -= OnVitalsUpdated;
                gameStateClient.PartyUpdated -= OnPartyUpdated;
                gameStateClient.AbilityBarUpdated -= OnAbilityBarUpdated;
                gameStateClient.InteractionPromptUpdated -= OnPromptUpdated;
            }
        }

        private void OnVitalsUpdated(PlayerVitals vitals)
        {
            if (vitals == null)
            {
                return;
            }

            SetSlider(healthBar, vitals.HealthFraction);
            SetSlider(manaBar, vitals.ManaFraction);
            SetSlider(experienceBar, vitals.ExperienceFraction);

            if (levelLabel != null)
            {
                levelLabel.text = $"Lv {vitals.Level}";
            }
        }

        private void OnPartyUpdated(PartySnapshot snapshot)
        {
            if (partyList == null || snapshot?.Members == null)
            {
                return;
            }

            var builder = new StringBuilder();
            foreach (var member in snapshot.Members)
            {
                var leaderTag = member.IsLeader ? " (Leader)" : string.Empty;
                builder.AppendLine($"{member.Name}{leaderTag} - {Mathf.RoundToInt(member.HealthFraction * 100)}%");
            }

            partyList.text = builder.ToString();
        }

        private void OnAbilityBarUpdated(AbilityBarState abilityBar)
        {
            if (abilityBar == null || abilityBar.Slots == null)
            {
                return;
            }

            for (int i = 0; i < abilityBar.Slots.Length && i < abilityIcons.Length; i++)
            {
                var slot = abilityBar.Slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (abilityIcons[i] != null)
                {
                    abilityIcons[i].sprite = slot.Icon;
                    abilityIcons[i].enabled = slot.Icon != null;
                }

                if (cooldownMasks != null && i < cooldownMasks.Length && cooldownMasks[i] != null)
                {
                    cooldownMasks[i].fillAmount = slot.OnCooldown ? slot.CooldownFraction : 0f;
                }
            }
        }

        private void OnPromptUpdated(InteractionPrompt prompt)
        {
            if (promptRoot != null)
            {
                promptRoot.SetActive(prompt != null && prompt.IsVisible);
            }

            if (promptText != null && prompt != null)
            {
                promptText.text = prompt.Prompt;
            }
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.value = Mathf.Clamp01(value);
            }
        }
    }
}
