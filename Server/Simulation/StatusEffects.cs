using System;
using System.Collections.Generic;
using System.Linq;

namespace Adventure.Server.Simulation
{
    public enum StatusStackBehavior
    {
        Refresh,
        Extend,
        Stack
    }

    [Flags]
    public enum StatusFlags
    {
        None = 0,
        IsBuff = 1 << 0,
        IsDebuff = 1 << 1,
        CrowdControl = 1 << 2,
        Silence = 1 << 3,
        Root = 1 << 4
    }

    public enum DispelType
    {
        Magic,
        Curse,
        Poison,
        Physical
    }

    public class StatusEffectDefinition
    {
        public string Id { get; init; } = string.Empty;

        public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(5);

        public TimeSpan? PeriodicInterval { get; init; }

        public int MaxStacks { get; init; } = 1;

        public StatusStackBehavior StackBehavior { get; init; } = StatusStackBehavior.Refresh;

        public StatusFlags Flags { get; init; } = StatusFlags.None;

        public IReadOnlyCollection<DispelType> DispelTypes { get; init; } = Array.Empty<DispelType>();

        public Action<SimulatedActor>? OnApplied { get; init; }

        public Action<SimulatedActor>? OnRemoved { get; init; }

        public Action<SimulatedActor, int>? OnPeriodicTick { get; init; }
    }

    public class StatusEffectInstance
    {
        public StatusEffectDefinition Definition { get; }

        public TimeSpan Remaining { get; private set; }

        public TimeSpan PeriodicRemaining { get; private set; }

        public int Stacks { get; private set; }

        public StatusEffectInstance(StatusEffectDefinition definition)
        {
            Definition = definition;
            Remaining = definition.Duration;
            PeriodicRemaining = definition.PeriodicInterval ?? TimeSpan.Zero;
            Stacks = 1;
        }

        public void Refresh()
        {
            Remaining = Definition.Duration;
            PeriodicRemaining = Definition.PeriodicInterval ?? TimeSpan.Zero;
        }

        public void Extend()
        {
            Remaining += Definition.Duration;
        }

        public void AddStack()
        {
            if (Stacks < Definition.MaxStacks)
            {
                Stacks++;
            }
            Refresh();
        }

        public bool Tick(TimeSpan delta, SimulatedActor owner)
        {
            Remaining -= delta;
            if (Definition.PeriodicInterval.HasValue)
            {
                PeriodicRemaining -= delta;
                if (PeriodicRemaining <= TimeSpan.Zero)
                {
                    Definition.OnPeriodicTick?.Invoke(owner, Stacks);
                    PeriodicRemaining = Definition.PeriodicInterval.Value;
                }
            }

            return Remaining > TimeSpan.Zero;
        }

        public bool IsDispellableBy(DispelType type)
        {
            return Definition.DispelTypes.Contains(type);
        }
    }

    public class StatusEffectContainer
    {
        private readonly Dictionary<string, StatusEffectInstance> active = new();

        public IReadOnlyCollection<StatusEffectInstance> Active => active.Values;

        public void Apply(StatusEffectDefinition definition, SimulatedActor owner)
        {
            if (active.TryGetValue(definition.Id, out var existing))
            {
                switch (definition.StackBehavior)
                {
                    case StatusStackBehavior.Refresh:
                        existing.Refresh();
                        break;
                    case StatusStackBehavior.Extend:
                        existing.Extend();
                        break;
                    case StatusStackBehavior.Stack:
                        existing.AddStack();
                        break;
                }
            }
            else
            {
                var instance = new StatusEffectInstance(definition);
                active[definition.Id] = instance;
                definition.OnApplied?.Invoke(owner);
            }
        }

        public void Dispel(DispelType type, SimulatedActor owner)
        {
            var dispelled = active.Values.Where(e => e.IsDispellableBy(type)).ToList();
            foreach (var instance in dispelled)
            {
                active.Remove(instance.Definition.Id);
                instance.Definition.OnRemoved?.Invoke(owner);
            }
        }

        public void Tick(TimeSpan delta, SimulatedActor owner)
        {
            var expired = new List<string>();
            foreach (var instance in active.Values)
            {
                var alive = instance.Tick(delta, owner);
                if (!alive)
                {
                    expired.Add(instance.Definition.Id);
                }
            }

            foreach (var id in expired)
            {
                if (active.TryGetValue(id, out var instance))
                {
                    active.Remove(id);
                    instance.Definition.OnRemoved?.Invoke(owner);
                }
            }
        }
    }
}
