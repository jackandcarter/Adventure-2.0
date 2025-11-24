using System.Collections.Generic;
using System.Text;
using Adventure.GameData.Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure.UI.Hotbar
{
    public class HotbarAbilitySlot : MonoBehaviour
    {
        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TMP_Text nameLabel;

        [SerializeField]
        private TMP_Text cooldownLabel;

        [SerializeField]
        private TMP_Text costLabel;

        public void SetAbility(AbilityDefinition ability)
        {
            if (ability == null)
            {
                Clear();
                return;
            }

            if (iconImage != null)
            {
                iconImage.sprite = ability.Icon;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (nameLabel != null)
            {
                nameLabel.text = ability.DisplayName;
            }

            if (cooldownLabel != null)
            {
                cooldownLabel.text = $"{ability.CooldownSeconds:0.0}s";
            }

            if (costLabel != null)
            {
                costLabel.text = BuildCostText(ability.ResourceCosts);
            }
        }

        public void Clear()
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (nameLabel != null)
            {
                nameLabel.text = string.Empty;
            }

            if (cooldownLabel != null)
            {
                cooldownLabel.text = string.Empty;
            }

            if (costLabel != null)
            {
                costLabel.text = string.Empty;
            }
        }

        private static string BuildCostText(IReadOnlyCollection<ResourceCost> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return "Free";
            }

            StringBuilder builder = new StringBuilder();
            bool first = true;
            foreach (ResourceCost cost in costs)
            {
                if (cost == null)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(cost.Amount.ToString("0"));
                builder.Append(' ');
                builder.Append(string.IsNullOrWhiteSpace(cost.ResourceId) ? "Resource" : cost.ResourceId);
                first = false;
            }

            return builder.Length > 0 ? builder.ToString() : "Free";
        }
    }
}
