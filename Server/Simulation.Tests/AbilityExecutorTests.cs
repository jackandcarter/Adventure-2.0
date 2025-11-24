using System;
using System.Collections.Generic;
using System.Numerics;
using Adventure.Server.Simulation;
using FluentAssertions;
using Xunit;

namespace Adventure.Server.Simulation.Tests
{
    public class AbilityExecutorTests
    {
        private readonly AbilityCatalog catalog = new();
        private readonly RoomLayout layout = new(new Vector2(20, 20), 1f, Array.Empty<(int, int)>());

        [Fact]
        public void Validate_Fails_WhenOnCooldown()
        {
            var ability = new AbilityDefinition
            {
                Id = "fireball",
                Cooldown = TimeSpan.FromSeconds(5),
                Timing = AbilityTiming.Instant,
                CastTime = TimeSpan.Zero,
                ChannelDuration = TimeSpan.Zero,
                Cost = new AbilityCost { Amount = 0, ResourceType = AbilityResourceType.Mana }
            };
            catalog.Register(ability);
            var executor = new AbilityExecutor(catalog, layout);
            var caster = new SimulatedActor("caster", Vector3.Zero, new StatSnapshot());
            var now = DateTime.UtcNow;
            caster.SetCooldown(ability.Id, now.AddSeconds(2));

            var isValid = executor.Validate(new AbilityCastCommand(ability.Id, string.Empty, Vector3.Zero, now), caster, null, now, out var denial);

            isValid.Should().BeFalse();
            denial.Should().Be("cooldown");
        }

        [Fact]
        public void Resolve_SpendsResources_And_SetsCooldown()
        {
            var ability = new AbilityDefinition
            {
                Id = "arcane_bolt",
                Cooldown = TimeSpan.FromSeconds(3),
                Timing = AbilityTiming.Instant,
                CastTime = TimeSpan.Zero,
                ChannelDuration = TimeSpan.Zero,
                Cost = new AbilityCost { Amount = 10, ResourceType = AbilityResourceType.Mana },
                Power = 1f
            };
            catalog.Register(ability);
            var executor = new AbilityExecutor(catalog, layout);
            var casterStats = new StatSnapshot { MaxMana = 50, MagicPower = 5f };
            var targetStats = new StatSnapshot { MaxHealth = 100 };
            var caster = new SimulatedActor("caster", Vector3.Zero, casterStats);
            var target = new SimulatedActor("target", Vector3.UnitZ, targetStats);
            var now = DateTime.UtcNow;

            executor.Resolve(ability, caster, target, Vector3.UnitZ, now);

            caster.Resources.Mana.Should().Be(casterStats.MaxMana - ability.Cost.Amount);
            caster.IsOnCooldown(ability.Id, now.AddMilliseconds(10)).Should().BeTrue();
            executor.Results.Should().ContainSingle();
        }

        [Fact]
        public void StatResolver_Computes_Scaled_Values()
        {
            var resolver = new StatResolver();
            var statBlock = new StatBlockDefinition
            {
                Id = "mage",
                Stats = new List<StatGrowthDefinition>
                {
                    new()
                    {
                        StatId = nameof(StatSnapshot.AttackPower),
                        BaseValue = 10,
                        Curve = new List<StatCurvePoint>
                        {
                            new(1, 10),
                            new(5, 15),
                            new(10, 20)
                        }
                    },
                    new()
                    {
                        StatId = nameof(StatSnapshot.MaxHealth),
                        BaseValue = 80,
                        UseFormula = true,
                        Formula = "{base} + {level} * 2"
                    }
                }
            };

            var gear = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [nameof(StatSnapshot.AttackPower)] = 5f
            };

            var snapshot = resolver.Resolve(statBlock, level: 5, gearBonuses: gear);

            snapshot.AttackPower.Should().BeApproximately(20f, 0.001f);
            snapshot.MaxHealth.Should().Be(90f);
        }
    }
}
