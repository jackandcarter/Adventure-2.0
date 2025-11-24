using System;
using System.Collections.Generic;
using System.Numerics;

namespace Adventure.Server.Simulation
{
    public enum AbilityTiming
    {
        Instant,
        Cast,
        Channel
    }

    public enum AbilityCategory
    {
        Standard,
        Trance
    }

    public enum AbilityResourceType
    {
        Mana,
        Trance
    }

    public class AbilityCost
    {
        public AbilityResourceType ResourceType { get; init; }

        public float Amount { get; init; }
    }

    public class AbilityDefinition
    {
        public string Id { get; init; } = string.Empty;

        public float Range { get; init; } = 25f;

        public TimeSpan Cooldown { get; init; } = TimeSpan.FromSeconds(5);

        public TimeSpan CastTime { get; init; } = TimeSpan.Zero;

        public TimeSpan ChannelDuration { get; init; } = TimeSpan.Zero;

        public AbilityTiming Timing { get; init; } = AbilityTiming.Instant;

        public AbilityCategory Category { get; init; } = AbilityCategory.Standard;

        public bool RequiresLineOfSight { get; init; } = true;

        public AbilityCost Cost { get; init; } = new AbilityCost { ResourceType = AbilityResourceType.Mana, Amount = 10 };

        public Element Element { get; init; } = Element.Physical;

        public float Power { get; init; } = 1f;

        public bool IsHealing { get; init; }

        public float MinimumTrance { get; init; }

        public TranceBehavior TranceBehavior { get; init; } = TranceBehavior.None;
    }

    public enum TranceBehavior
    {
        None,
        DoubleCast
    }

    public record AbilityCastCommand(string AbilityId, string TargetId, Vector3 TargetPosition, DateTime Timestamp);

    public record AbilityCastState(AbilityDefinition Definition, DateTime StartedAt, string TargetId, Vector3 TargetPosition);

    public class AbilityCatalog
    {
        private readonly Dictionary<string, AbilityDefinition> definitions = new();

        public void Register(params AbilityDefinition[] abilityDefinitions)
        {
            foreach (var definition in abilityDefinitions)
            {
                definitions[definition.Id] = definition;
            }
        }

        public bool TryGet(string abilityId, out AbilityDefinition definition) => definitions.TryGetValue(abilityId, out definition!);
    }

    public interface IAbilityExecutor
    {
        IReadOnlyCollection<AbilityExecutionResult> Results { get; }

        bool Validate(AbilityCastCommand command, SimulatedActor caster, SimulatedActor? target, DateTime now, out string denial);

        void StartCast(AbilityCastCommand command, SimulatedActor caster, SimulatedActor? target, DateTime now);

        void UpdateCasting(TimeSpan delta, DateTime now, IEnumerable<SimulatedActor> actors);

        void Resolve(AbilityDefinition definition, SimulatedActor caster, SimulatedActor? target, Vector3 targetPosition, DateTime now);

        void UpdateChannels(TimeSpan delta, DateTime now, IEnumerable<SimulatedActor> actors);

        void ClearResults();
    }

    public class AbilityExecutionResult
    {
        public string AbilityId { get; init; } = string.Empty;

        public string SourceId { get; init; } = string.Empty;

        public string? TargetId { get; init; }

        public int Amount { get; init; }

        public bool IsHeal { get; init; }

        public Element Element { get; init; }

        public bool Crit { get; init; }
    }

    public class AbilityExecutor : IAbilityExecutor
    {
        private readonly AbilityCatalog catalog;
        private readonly RoomLayout layout;
        private readonly List<AbilityExecutionResult> bufferedResults = new();

        public IReadOnlyCollection<AbilityExecutionResult> Results => bufferedResults;

        public AbilityExecutor(AbilityCatalog catalog, RoomLayout layout)
        {
            this.catalog = catalog;
            this.layout = layout;
        }

        public bool Validate(AbilityCastCommand command, SimulatedActor caster, SimulatedActor? target, DateTime now, out string denial)
        {
            denial = string.Empty;
            if (!catalog.TryGet(command.AbilityId, out var definition))
            {
                denial = "unknown_ability";
                return false;
            }

            if (caster.IsOnCooldown(command.AbilityId, now))
            {
                denial = "cooldown";
                return false;
            }

            if (definition.Category == AbilityCategory.Trance && !HasRequiredTrance(caster, definition))
            {
                denial = "trance_locked";
                return false;
            }

            if (!HasResources(caster, definition))
            {
                denial = "resources";
                return false;
            }

            if (target != null && !IsInRange(definition.Range, caster.Position, target.Position))
            {
                denial = "range";
                return false;
            }

            if (definition.RequiresLineOfSight && target != null && !layout.HasLineOfSight(caster.Position, target.Position))
            {
                denial = "line_of_sight";
                return false;
            }

            return true;
        }

        public void StartCast(AbilityCastCommand command, SimulatedActor caster, SimulatedActor? target, DateTime now)
        {
            if (!catalog.TryGet(command.AbilityId, out var definition))
            {
                return;
            }

            caster.BeginCast(new AbilityCastState(definition, now, command.TargetId, command.TargetPosition));
        }

        public void UpdateCasting(TimeSpan delta, DateTime now, IEnumerable<SimulatedActor> actors)
        {
            foreach (var actor in actors)
            {
                actor.TickCasting(delta, now, (definition, targetId, targetPosition) =>
                {
                    var target = actor.Room?.FindActor(targetId);
                    Resolve(definition, actor, target, targetPosition, now);
                });
            }
        }

        public void Resolve(AbilityDefinition definition, SimulatedActor caster, SimulatedActor? target, Vector3 targetPosition, DateTime now)
        {
            caster.CommitResourceCost(definition.Cost);
            caster.SetCooldown(definition.Id, now + definition.Cooldown);

            if (definition.Timing == AbilityTiming.Channel && definition.ChannelDuration > TimeSpan.Zero)
            {
                caster.BeginChannel(definition, now, target?.ActorId, targetPosition);
                return;
            }

            ExecuteWithBehavior(definition, caster, target, targetPosition);
        }

        public void UpdateChannels(TimeSpan delta, DateTime now, IEnumerable<SimulatedActor> actors)
        {
            foreach (var actor in actors)
            {
                actor.TickChannel(delta, now, definition => ExecuteWithBehavior(definition, actor, actor.Room?.FindActor(actor.ChannelTargetId), actor.ChannelTargetPosition));
            }
        }

        private void ExecuteWithBehavior(AbilityDefinition definition, SimulatedActor caster, SimulatedActor? target, Vector3 targetPosition)
        {
            switch (definition.TranceBehavior)
            {
                case TranceBehavior.DoubleCast:
                    ExecuteSingle(definition, caster, target, targetPosition);
                    ExecuteSingle(definition, caster, target, targetPosition);
                    break;
                default:
                    ExecuteSingle(definition, caster, target, targetPosition);
                    break;
            }
        }

        private void ExecuteSingle(AbilityDefinition definition, SimulatedActor caster, SimulatedActor? target, Vector3 targetPosition)
        {
            if (target == null)
            {
                return;
            }

            var stats = caster.StatsSnapshot;
            var derived = stats.ToDerived();
            var damage = CalculateAmount(definition, stats, derived, target.StatsSnapshot);
            var crit = RollCrit(derived);
            if (crit)
            {
                damage = (int)(damage * derived.CriticalDamageMultiplier);
            }

            var signedAmount = definition.IsHealing ? Math.Abs(damage) : -Math.Abs(damage);
            target.ApplyCombatResult(signedAmount, definition.Element);
            caster.Resources.Trance.AccrueFromDamage(Math.Abs(damage), 1f + stats.TranceGenerationBonus);

            bufferedResults.Add(new AbilityExecutionResult
            {
                AbilityId = definition.Id,
                SourceId = caster.ActorId,
                TargetId = target.ActorId,
                Amount = Math.Abs(damage),
                IsHeal = definition.IsHealing,
                Element = definition.Element,
                Crit = crit
            });
        }

        private static bool HasResources(SimulatedActor caster, AbilityDefinition definition)
        {
            return definition.Cost.ResourceType switch
            {
                AbilityResourceType.Mana => caster.Resources.Mana >= definition.Cost.Amount,
                AbilityResourceType.Trance => caster.Resources.Trance.CanSpend(definition.Cost.Amount * caster.StatsSnapshot.TranceSpendEfficiency),
                _ => true
            };
        }

        private static bool HasRequiredTrance(SimulatedActor caster, AbilityDefinition definition)
        {
            var tranceAvailable = caster.Resources.Trance.Current;
            var tranceCost = definition.Cost.ResourceType == AbilityResourceType.Trance
                ? definition.Cost.Amount * caster.StatsSnapshot.TranceSpendEfficiency
                : 0f;

            return tranceAvailable >= Math.Max(definition.MinimumTrance, tranceCost);
        }

        private static bool IsInRange(float range, Vector3 origin, Vector3 target)
        {
            return Vector3.Distance(origin, target) <= range;
        }

        private static int CalculateAmount(AbilityDefinition definition, StatSnapshot stats, DerivedStats derived, StatSnapshot targetStats)
        {
            var attackScaling = definition.IsHealing ? stats.MagicPower : stats.AttackPower;
            var mitigation = definition.IsHealing ? 0f : definition.Element switch
            {
                Element.Physical => targetStats.Defense,
                Element.Fire or Element.Ice or Element.Lightning or Element.Arcane => targetStats.MagicResist,
                _ => 0f
            };

            var baseAmount = definition.Power * attackScaling;
            var mitigated = Math.Max(1f, baseAmount - mitigation * 0.5f);
            return (int)MathF.Round(mitigated);
        }

        private static bool RollCrit(DerivedStats derived)
        {
            return Random.Shared.NextDouble() <= derived.CriticalChance;
        }

        public void ClearResults()
        {
            bufferedResults.Clear();
        }
    }
}
