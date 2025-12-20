using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Adventure.Shared.Network.Messages;

namespace Adventure.Server.Simulation
{
    public class SimulatedActor
    {
        private readonly ConcurrentQueue<object> commandQueue = new();
        private readonly Dictionary<string, DateTime> cooldowns = new();
        private AbilityCastState? currentCast;
        private DateTime castCompleteAt;
        private AbilityDefinition? activeChannel;
        private DateTime channelEndAt;
        private string? channelTargetId;
        private Vector3 channelTargetPosition;

        public string ActorId { get; init; } = string.Empty;

        public Vector3 Position { get; private set; }

        public Vector3 Direction { get; private set; } = Vector3.UnitZ;

        public StatSnapshot StatsSnapshot { get; private set; } = new();

        public ResourceState Resources { get; private set; } = new(100, 50);

        public StatusEffectContainer Effects { get; } = new();

        public SimulationRoom? Room { get; internal set; }

        public SimulatedActor(string actorId, Vector3 spawnPosition, StatSnapshot stats)
        {
            ActorId = actorId;
            Position = spawnPosition;
            StatsSnapshot = stats;
            Resources = new ResourceState(stats.MaxHealth, stats.MaxMana);
        }

        public void EnqueueCommand(object command)
        {
            commandQueue.Enqueue(command);
        }

        public bool TryDequeueCommand(out object? command)
        {
            var has = commandQueue.TryDequeue(out var dequeued);
            command = dequeued;
            return has;
        }

        public bool IsOnCooldown(string abilityId, DateTime now)
        {
            return cooldowns.TryGetValue(abilityId, out var until) && until > now;
        }

        public void SetCooldown(string abilityId, DateTime until)
        {
            cooldowns[abilityId] = until;
        }

        public void BeginCast(AbilityCastState state)
        {
            currentCast = state;
            castCompleteAt = state.StartedAt + state.Definition.CastTime;
        }

        public void TickCasting(TimeSpan delta, DateTime now, Action<AbilityDefinition, string, Vector3> onComplete)
        {
            if (currentCast == null)
            {
                return;
            }

            if (now < castCompleteAt)
            {
                return;
            }

            onComplete(currentCast.Definition, currentCast.TargetId, currentCast.TargetPosition);
            currentCast = null;
        }

        public void BeginChannel(AbilityDefinition definition, DateTime now, string? targetId, Vector3 targetPosition)
        {
            activeChannel = definition;
            channelEndAt = now + definition.ChannelDuration;
            channelTargetId = targetId;
            channelTargetPosition = targetPosition;
        }

        public void TickChannel(TimeSpan delta, DateTime now, Action<AbilityDefinition> onComplete)
        {
            if (activeChannel == null)
            {
                return;
            }

            if (now < channelEndAt)
            {
                return;
            }

            onComplete(activeChannel);
            activeChannel = null;
            channelTargetId = null;
        }

        public string? ChannelTargetId => channelTargetId;

        public Vector3 ChannelTargetPosition => channelTargetPosition;

        public void ApplyCombatResult(float deltaHealth, Element element)
        {
            if (deltaHealth < 0)
            {
                Resources.Trance.AccrueFromDamage(Math.Abs(deltaHealth), 1f + StatsSnapshot.TranceGenerationBonus);
            }

            Resources.ApplyDelta(deltaHealth, 0);
        }

        public void CommitResourceCost(AbilityCost cost)
        {
            switch (cost.ResourceType)
            {
                case AbilityResourceType.Mana:
                    Resources.Mana = Math.Max(0, Resources.Mana - cost.Amount);
                    break;
                case AbilityResourceType.Trance:
                    Resources.Trance.Spend(cost.Amount * StatsSnapshot.TranceSpendEfficiency);
                    break;
            }
        }

        public void TickStatusEffects(TimeSpan delta)
        {
            Effects.Tick(delta, this);
        }

        public void SetDirection(Vector3 direction)
        {
            if (direction.LengthSquared() > 0)
            {
                Direction = Vector3.Normalize(direction);
            }
        }

        public void SetPosition(Vector3 position)
        {
            Position = position;
        }
    }

    public class SimulationRoom
    {
        private readonly IAbilityExecutor abilityExecutor;
        private readonly TimeSpan movementReconciliationGrace = TimeSpan.FromMilliseconds(200);
        private readonly ConcurrentQueue<SimulationOutput> outputQueue = new();

        public string RoomId { get; }

        public RoomLayout Layout { get; }

        public IReadOnlyCollection<SimulatedActor> Actors => actors.Values;

        private readonly Dictionary<string, SimulatedActor> actors = new();

        public SimulationRoom(string roomId, RoomLayout layout, AbilityCatalog catalog, Random? random = null)
        {
            RoomId = roomId;
            Layout = layout;
            abilityExecutor = new AbilityExecutor(catalog, layout, random);
        }

        public SimulatedActor Spawn(string actorId, Vector3 position, StatSnapshot stats)
        {
            var actor = new SimulatedActor(actorId, position, stats) { Room = this };
            actors[actorId] = actor;
            return actor;
        }

        public SimulatedActor? FindActor(string actorId)
        {
            return actors.TryGetValue(actorId, out var actor) ? actor : null;
        }

        public void Tick(TimeSpan delta, DateTime now)
        {
            foreach (var actor in actors.Values)
            {
                actor.Resources.Trance.Tick(delta);
                actor.TickStatusEffects(delta);
                ProcessCommands(actor, delta, now);
            }

            abilityExecutor.UpdateCasting(delta, now, actors.Values);
            abilityExecutor.UpdateChannels(delta, now, actors.Values);
            EmitCombatEvents();
        }

        private void ProcessCommands(SimulatedActor actor, TimeSpan delta, DateTime now)
        {
            while (actor.TryDequeueCommand(out var raw))
            {
                switch (raw)
                {
                    case MovementCommand movement:
                        ReconcileMovement(actor, movement, delta);
                        break;
                    case AbilityCastCommand abilityCast:
                        HandleAbilityCommand(actor, abilityCast, now, null, null);
                        break;
                    case AbilityCastInput abilityCastInput:
                        HandleAbilityCommand(actor, abilityCastInput.Command, now, abilityCastInput.SessionId, abilityCastInput.RequestId);
                        break;
                }
            }
        }

        private void ReconcileMovement(SimulatedActor actor, MovementCommand command, TimeSpan delta)
        {
            var desiredDirection = command.Direction;
            actor.SetDirection(desiredDirection);

            var speed = command.IsSprinting ? command.Speed * 1.2f : command.Speed;
            var displacement = desiredDirection * speed * (float)delta.TotalSeconds;
            var predictedPosition = actor.Position + displacement;

            if (Vector3.Distance(command.Position, actor.Position) > speed * (float)movementReconciliationGrace.TotalSeconds)
            {
                actor.SetPosition(command.Position);
            }

            if (!Layout.TryResolveMovement(actor.Position, predictedPosition, out var resolved))
            {
                return;
            }

            actor.SetPosition(resolved);
            outputQueue.Enqueue(new MovementOutput(actor.ActorId, new MovementCommand
            {
                Position = actor.Position,
                Direction = actor.Direction,
                Speed = command.Speed,
                IsSprinting = command.IsSprinting
            }));
        }

        private void HandleAbilityCommand(SimulatedActor actor, AbilityCastCommand command, DateTime now, string? sessionId, string? requestId)
        {
            var target = FindActor(command.TargetId);
            if (!abilityExecutor.Validate(command, actor, target, now, out var denial))
            {
                outputQueue.Enqueue(new AbilityCastOutput(actor.ActorId, new AbilityCastResult
                {
                    AbilityId = command.AbilityId,
                    Accepted = false,
                    DenialReason = denial
                }, sessionId, requestId));
                return;
            }

            if (command.Timestamp > now + movementReconciliationGrace)
            {
                outputQueue.Enqueue(new AbilityCastOutput(actor.ActorId, new AbilityCastResult
                {
                    AbilityId = command.AbilityId,
                    Accepted = false,
                    DenialReason = "timestamp"
                }, sessionId, requestId));
                return;
            }

            if (command.AbilityId is { Length: 0 })
            {
                outputQueue.Enqueue(new AbilityCastOutput(actor.ActorId, new AbilityCastResult
                {
                    AbilityId = command.AbilityId,
                    Accepted = false,
                    DenialReason = "empty_ability"
                }, sessionId, requestId));
                return;
            }

            if (command.AbilityId == null)
            {
                outputQueue.Enqueue(new AbilityCastOutput(actor.ActorId, new AbilityCastResult
                {
                    AbilityId = string.Empty,
                    Accepted = false,
                    DenialReason = "missing_ability"
                }, sessionId, requestId));
                return;
            }

            abilityExecutor.StartCast(command, actor, target, now);
            outputQueue.Enqueue(new AbilityCastOutput(actor.ActorId, new AbilityCastResult
            {
                AbilityId = command.AbilityId,
                Accepted = true,
                DenialReason = string.Empty
            }, sessionId, requestId));
        }

        private void EmitCombatEvents()
        {
            foreach (var result in abilityExecutor.Results)
            {
                outputQueue.Enqueue(new CombatOutput(new CombatEvent
                {
                    SourceId = result.SourceId,
                    TargetId = result.TargetId ?? string.Empty,
                    Amount = result.Amount,
                    AbilityId = result.AbilityId,
                    EventType = result.IsHeal ? CombatEventType.Heal : CombatEventType.Damage
                }));
            }

            abilityExecutor.ClearResults();
        }

        public IReadOnlyCollection<SimulationOutput> ConsumeOutputs()
        {
            var outputs = new List<SimulationOutput>();
            while (outputQueue.TryDequeue(out var output))
            {
                outputs.Add(output);
            }

            return outputs;
        }
    }
}
