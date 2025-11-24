using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using UnityEngine;

namespace Adventure.GameData.Definitions
{
    [CreateAssetMenu(menuName = "Adventure/Definitions/Stat Block", fileName = "NewStatBlock")]
    public class StatBlock : ScriptableObject, IIdentifiableDefinition
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
        private List<StatGrowth> stats = new List<StatGrowth>();

        public string Id => id;

        public string DisplayName => displayName;

        public string Description => description;

        public Sprite Icon => icon;

        public IReadOnlyList<StatGrowth> Stats => stats;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId;
            displayName = newDisplayName;
        }
    }

    [Serializable]
    public class StatGrowth
    {
        [SerializeField]
        private string statId;

        [SerializeField]
        private float baseValue = 1f;

        [SerializeField]
        private AnimationCurve growthCurve = AnimationCurve.Linear(1f, 1f, 50f, 50f);

        [SerializeField]
        private bool useFormula;

        [SerializeField]
        private string formula = "{base} + ({level}-1)";

        public string StatId => statId;

        public float BaseValue => baseValue;

        public AnimationCurve GrowthCurve => growthCurve;

        public bool UseFormula => useFormula;

        public string Formula => formula;

        public float Evaluate(int level)
        {
            return DefinitionMath.EvaluateCurveOrFormula(baseValue, growthCurve, useFormula, formula, level);
        }
    }

    public static class DefinitionMath
    {
        public static float EvaluateCurveOrFormula(float baseValue, AnimationCurve curve, bool useFormula, string formula, int level)
        {
            if (useFormula && !string.IsNullOrWhiteSpace(formula))
            {
                try
                {
                    string formatted = formula
                        .Replace("{base}", baseValue.ToString(CultureInfo.InvariantCulture))
                        .Replace("{level}", level.ToString(CultureInfo.InvariantCulture));

                    using DataTable table = new DataTable();
                    object raw = table.Compute(formatted, string.Empty);
                    return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to evaluate formula '{formula}' at level {level}: {ex.Message}");
                }
            }

            return curve != null ? curve.Evaluate(level) : baseValue;
        }
    }
}
