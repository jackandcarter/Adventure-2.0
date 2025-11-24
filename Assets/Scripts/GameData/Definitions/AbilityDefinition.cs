using System;
using System.Collections.Generic;
using UnityEngine;

namespace Adventure.GameData.Definitions
{
    [CreateAssetMenu(menuName = "Adventure/Definitions/Ability Definition", fileName = "NewAbilityDefinition")]
    public class AbilityDefinition : ScriptableObject, IIdentifiableDefinition
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private string displayName;

        [TextArea]
        [SerializeField]
        private string description;

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private float cooldownSeconds = 1f;

        [SerializeField]
        private List<ResourceCost> resourceCosts = new List<ResourceCost>();

        [SerializeField]
        private List<string> tags = new List<string>();

        [Header("Effect")]
        [SerializeField]
        private float baseEffectValue = 1f;

        [SerializeField]
        private AnimationCurve effectGrowthCurve = AnimationCurve.Linear(1f, 1f, 10f, 10f);

        [SerializeField]
        private bool useEffectFormula;

        [SerializeField]
        private string effectFormula = "{base} + {level}";

        [TextArea]
        [SerializeField]
        private string effectSummary;

        public string Id => id;

        public string DisplayName => displayName;

        public string Description => description;

        public Sprite Icon => icon;

        public float CooldownSeconds => cooldownSeconds;

        public IReadOnlyList<ResourceCost> ResourceCosts => resourceCosts;

        public IReadOnlyList<string> Tags => tags;

        public float BaseEffectValue => baseEffectValue;

        public AnimationCurve EffectGrowthCurve => effectGrowthCurve;

        public bool UseEffectFormula => useEffectFormula;

        public string EffectFormula => effectFormula;

        public string EffectSummary => effectSummary;

        public float EvaluateEffectAtLevel(int level)
        {
            return DefinitionMath.EvaluateCurveOrFormula(baseEffectValue, effectGrowthCurve, useEffectFormula, effectFormula, level);
        }

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId;
            displayName = newDisplayName;
        }
    }

    [Serializable]
    public class ResourceCost
    {
        [SerializeField]
        private string resourceId;

        [SerializeField]
        private float amount = 1f;

        public string ResourceId => resourceId;

        public float Amount => amount;
    }
}
