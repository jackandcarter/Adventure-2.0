using System;

namespace Adventure.Server.Simulation
{
    public enum Element
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Arcane,
        Holy,
        Shadow
    }

    public class DerivedStats
    {
        public float CriticalChance { get; init; }

        public float CriticalDamageMultiplier { get; init; }

        public float EvadeChance { get; init; }

        public float MovementSpeed { get; init; }
    }

    public class StatSnapshot
    {
        public int Level { get; set; } = 1;

        public float MaxHealth { get; set; } = 100f;

        public float MaxMana { get; set; } = 50f;

        public float AttackPower { get; set; } = 10f;

        public float MagicPower { get; set; } = 10f;

        public float Speed { get; set; } = 1f;

        public float Defense { get; set; } = 0f;

        public float MagicResist { get; set; } = 0f;

        public float Tenacity { get; set; } = 0f;

        public float Precision { get; set; } = 0f;

        public float Awareness { get; set; } = 0f;

        public float CritRating { get; set; } = 0f;

        public float EvadeRating { get; set; } = 0f;

        public float TranceGenerationBonus { get; set; }

        public float TranceSpendEfficiency { get; set; } = 1f;

        public DerivedStats ToDerived()
        {
            var critChance = Math.Clamp(0.05f + CritRating * 0.0005f + Precision * 0.00025f, 0f, 0.75f);
            var critDamage = 1.5f + Precision * 0.0015f;
            var evade = Math.Clamp(EvadeRating * 0.0005f + Awareness * 0.0002f, 0f, 0.5f);
            var movementSpeed = Math.Max(1f, Speed * 0.1f);

            return new DerivedStats
            {
                CriticalChance = critChance,
                CriticalDamageMultiplier = critDamage,
                EvadeChance = evade,
                MovementSpeed = movementSpeed
            };
        }

        public StatSnapshot Clone()
        {
            return (StatSnapshot)MemberwiseClone();
        }
    }

    public class ResourceState
    {
        public float Health { get; set; }

        public float Mana { get; set; }

        public TranceMeter Trance { get; } = new();

        public ResourceState(float health, float mana)
        {
            Health = health;
            Mana = mana;
        }

        public void ApplyDelta(float healthDelta, float manaDelta)
        {
            Health = MathF.Max(0, Health + healthDelta);
            Mana = MathF.Max(0, Mana + manaDelta);
        }
    }

    public class TranceMeter
    {
        public float Current { get; private set; }

        public float Maximum { get; set; } = 100f;

        public float PassiveGainPerSecond { get; set; } = 0f;

        public bool CanSpend(float cost) => Current >= cost;

        public void Spend(float cost)
        {
            if (!CanSpend(cost))
            {
                throw new InvalidOperationException("Insufficient trance to spend.");
            }

            Current -= cost;
        }

        public void Tick(TimeSpan deltaTime)
        {
            Accrue(PassiveGainPerSecond * (float)deltaTime.TotalSeconds);
        }

        public void AccrueFromDamage(float amount, float generationModifier = 1f)
        {
            var gain = amount * 0.05f * generationModifier;
            Accrue(gain);
        }

        public void Accrue(float amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Current = Math.Clamp(Current + amount, 0f, Maximum);
        }
    }
}
