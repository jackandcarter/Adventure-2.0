using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Adventure.Server.Simulation
{
    public record StatCurvePoint(int Level, float Value);

    public class StatGrowthDefinition
    {
        public string StatId { get; init; } = string.Empty;

        public float BaseValue { get; init; }

        public List<StatCurvePoint> Curve { get; init; } = new();

        public bool UseFormula { get; init; }

        public string Formula { get; init; } = string.Empty;
    }

    public class StatBlockDefinition
    {
        public string Id { get; init; } = string.Empty;

        public List<StatGrowthDefinition> Stats { get; init; } = new();
    }

    public class StatResolver
    {
        private readonly Dictionary<string, Action<StatSnapshot, float>> statAssignments = new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(StatSnapshot.MaxHealth)] = (snapshot, value) => snapshot.MaxHealth = value,
            [nameof(StatSnapshot.MaxMana)] = (snapshot, value) => snapshot.MaxMana = value,
            [nameof(StatSnapshot.AttackPower)] = (snapshot, value) => snapshot.AttackPower = value,
            [nameof(StatSnapshot.MagicPower)] = (snapshot, value) => snapshot.MagicPower = value,
            [nameof(StatSnapshot.Speed)] = (snapshot, value) => snapshot.Speed = value,
            [nameof(StatSnapshot.Defense)] = (snapshot, value) => snapshot.Defense = value,
            [nameof(StatSnapshot.MagicResist)] = (snapshot, value) => snapshot.MagicResist = value,
            [nameof(StatSnapshot.Tenacity)] = (snapshot, value) => snapshot.Tenacity = value,
            [nameof(StatSnapshot.Precision)] = (snapshot, value) => snapshot.Precision = value,
            [nameof(StatSnapshot.Awareness)] = (snapshot, value) => snapshot.Awareness = value,
            [nameof(StatSnapshot.CritRating)] = (snapshot, value) => snapshot.CritRating = value,
            [nameof(StatSnapshot.EvadeRating)] = (snapshot, value) => snapshot.EvadeRating = value,
            [nameof(StatSnapshot.TranceGenerationBonus)] = (snapshot, value) => snapshot.TranceGenerationBonus = value,
            [nameof(StatSnapshot.TranceSpendEfficiency)] = (snapshot, value) => snapshot.TranceSpendEfficiency = value
        };

        public StatSnapshot Resolve(StatBlockDefinition statBlock, int level, IReadOnlyDictionary<string, float>? gearBonuses = null)
        {
            var snapshot = new StatSnapshot { Level = level };
            if (statBlock?.Stats == null)
            {
                return snapshot;
            }

            foreach (var stat in statBlock.Stats.Where(s => !string.IsNullOrWhiteSpace(s.StatId)))
            {
                var value = EvaluateStat(stat, level);
                if (gearBonuses != null && gearBonuses.TryGetValue(stat.StatId, out var bonus))
                {
                    value += bonus;
                }

                ApplyStat(snapshot, stat.StatId, value);
            }

            return snapshot;
        }

        private void ApplyStat(StatSnapshot snapshot, string statId, float value)
        {
            if (statAssignments.TryGetValue(statId, out var setter))
            {
                setter(snapshot, value);
            }
        }

        private static float EvaluateStat(StatGrowthDefinition growth, int level)
        {
            if (growth.UseFormula && !string.IsNullOrWhiteSpace(growth.Formula))
            {
                try
                {
                    var formatted = growth.Formula
                        .Replace("{base}", growth.BaseValue.ToString(CultureInfo.InvariantCulture))
                        .Replace("{level}", level.ToString(CultureInfo.InvariantCulture));

                    using DataTable table = new();
                    var raw = table.Compute(formatted, string.Empty);
                    return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // ignore and fall back to curve evaluation
                }
            }

            if (growth.Curve.Count == 0)
            {
                return growth.BaseValue;
            }

            var sorted = growth.Curve.OrderBy(p => p.Level).ToList();
            if (level <= sorted[0].Level)
            {
                return sorted[0].Value;
            }

            for (var i = 1; i < sorted.Count; i++)
            {
                var previous = sorted[i - 1];
                var current = sorted[i];
                if (level <= current.Level)
                {
                    var t = (level - previous.Level) / (float)(current.Level - previous.Level);
                    return Lerp(previous.Value, current.Value, t);
                }
            }

            var last = sorted[^1];
            if (sorted.Count == 1)
            {
                return last.Value;
            }

            var penultimate = sorted[^2];
            return last.Value + (level - last.Level) * (last.Value - penultimate.Value) / Math.Max(1, last.Level - penultimate.Level);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0f, 1f);
        }
    }
}
