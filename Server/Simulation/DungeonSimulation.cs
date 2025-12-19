using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Adventure.Server.Core.Lobby;
using Adventure.Server.Core.Repositories;
using Adventure.Server.Generation;
using Adventure.Server.Network;
using Adventure.Server.Persistence;
using Adventure.Shared.Network.Messages;

namespace Adventure.Server.Simulation
{
    public class DungeonSimulationFactory : IDungeonSimulationFactory
    {
        private readonly SimulationLoop loop;
        private readonly AbilityCatalog abilities;
        private readonly EnemyArchetypeCatalog enemies;
        private readonly IDungeonRunRepository runRepository;

        public DungeonSimulationFactory(
            SimulationLoop loop,
            AbilityCatalog abilities,
            EnemyArchetypeCatalog enemies,
            IDungeonRunRepository runRepository)
        {
            this.loop = loop;
            this.abilities = abilities;
            this.enemies = enemies;
            this.runRepository = runRepository;
        }

        public IDungeonSimulation Create(string dungeonId)
        {
            return new DungeonSimulation(loop, abilities, enemies, runRepository, dungeonId);
        }
    }

    public class DungeonSimulation : IDungeonSimulation
    {
        private readonly SimulationLoop loop;
        private readonly AbilityCatalog abilities;
        private readonly EnemyArchetypeCatalog enemies;
        private readonly IDungeonRunRepository runRepository;
        private readonly string dungeonId;
        private readonly ConcurrentDictionary<string, PlayerConnection> connections = new();
        private readonly ConcurrentDictionary<string, byte> layoutSent = new();
        private readonly List<SimulatedActor> enemyActors = new();
        private readonly object initializationLock = new();
        private DungeonRunLogger? logger;
        private DungeonStateValidator? validator;
        private DungeonLayoutSummary? layoutSummary;
        private SimulationRoom? room;
        private string? runId;
        private string? activeRoomId;
        private long tickIndex;
        private bool roomCleared;
        private bool initialized;

        public DungeonSimulation(
            SimulationLoop loop,
            AbilityCatalog abilities,
            EnemyArchetypeCatalog enemies,
            IDungeonRunRepository runRepository,
            string dungeonId)
        {
            this.loop = loop;
            this.abilities = abilities;
            this.enemies = enemies;
            this.runRepository = runRepository;
            this.dungeonId = dungeonId;
        }

        public async Task RunAsync(string instanceId, string runDungeonId, IEnumerable<LobbyMemberPresence> members, CancellationToken cancellationToken)
        {
            Initialize(instanceId, runDungeonId, members);
            loop.RoomTicked += OnRoomTick;

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                loop.RoomTicked -= OnRoomTick;
                if (runId != null)
                {
                    runRepository.RecordEnd(runId);
                }
            }
        }

        public Task HandlePlayerEventAsync(string instanceId, string playerId, object evt, CancellationToken cancellationToken)
        {
            if (!initialized)
            {
                return Task.CompletedTask;
            }

            var payload = evt;
            string? sessionId = null;
            string? requestId = null;

            if (evt is PlayerEventEnvelope envelope)
            {
                payload = envelope.Payload;
                sessionId = envelope.SessionId;
                requestId = envelope.RequestId;
                connections[playerId] = new PlayerConnection(envelope.Sender, envelope.SessionId);
                _ = SendLayoutIfNeededAsync(playerId);
            }

            var actor = room?.FindActor(playerId);
            if (actor == null)
            {
                return Task.CompletedTask;
            }

            var currentTick = Interlocked.Read(ref tickIndex);

            switch (payload)
            {
                case MovementCommand movement:
                    actor.EnqueueCommand(movement);
                    logger?.AppendEvent(DungeonRunEventTypes.MovementInput, new MovementLogEntry(playerId, movement, currentTick));
                    break;
                case AbilityCastRequest cast:
                    var command = new AbilityCastCommand(cast.AbilityId, cast.TargetId, cast.TargetPosition, DateTime.UtcNow);
                    actor.EnqueueCommand(new AbilityCastInput(command, sessionId, requestId));
                    logger?.AppendEvent(DungeonRunEventTypes.AbilityInput, new AbilityCastLogEntry(playerId, cast, currentTick));
                    break;
                case AbilityCastCommand castCommand:
                    actor.EnqueueCommand(castCommand);
                    break;
                case AbilityCastInput castInput:
                    actor.EnqueueCommand(castInput);
                    break;
            }

            return Task.CompletedTask;
        }

        private void Initialize(string instanceId, string runDungeonId, IEnumerable<LobbyMemberPresence> members)
        {
            lock (initializationLock)
            {
                if (initialized)
                {
                    return;
                }

                var resolvedDungeonId = string.IsNullOrWhiteSpace(runDungeonId) ? dungeonId : runDungeonId;
                var runRecord = runRepository.RecordStart(instanceId, resolvedDungeonId, instanceId);
                runId = runRecord.RunId;
                logger = new DungeonRunLogger(runRepository, runId);

                var seed = Math.Abs(HashCode.Combine(instanceId, resolvedDungeonId, DateTime.UtcNow.Ticks));
                logger.AppendEvent(DungeonRunEventTypes.Seed, new SeedLogEntry(resolvedDungeonId, seed));

                var generator = new DungeonGenerator(seed);
                var template = DungeonTemplateDefaults.CreateDefault(resolvedDungeonId);
                var generated = generator.Generate(template, new DungeonGenerationSettings());

                validator = new DungeonStateValidator(generated);
                layoutSummary = validator.CreateLayoutSummary();

                activeRoomId = generated.Rooms.FirstOrDefault(r => RequiresEnemyClear(r.Archetype))?.RoomId
                               ?? generated.Rooms.FirstOrDefault()?.RoomId;

                var layout = new RoomLayout(new Vector2(12, 12), 1f, Array.Empty<(int, int)>());
                var random = new Random(seed);
                room = new SimulationRoom($"{instanceId}-room", layout, abilities, random);
                loop.RegisterRoom(room);

                SpawnPlayers(members);
                SpawnEnemy();

                initialized = true;
            }
        }

        private void SpawnPlayers(IEnumerable<LobbyMemberPresence> members)
        {
            if (room == null)
            {
                return;
            }

            var index = 0;
            foreach (var member in members)
            {
                var position = new Vector3(1 + index, 0, 1);
                room.Spawn(member.PlayerId, position, new StatSnapshot());
                index++;
            }
        }

        private void SpawnEnemy()
        {
            if (room == null)
            {
                return;
            }

            if (!enemies.TryGet("training-dummy", out var archetype))
            {
                archetype = new EnemyArchetypeDefinition
                {
                    Id = "training-dummy",
                    DisplayName = "Training Dummy",
                    Stats = new StatSnapshot { MaxHealth = 50f, MaxMana = 0 }
                };
            }

            var enemy = room.Spawn("enemy-1", new Vector3(4, 0, 4), archetype.Stats.Clone());
            enemyActors.Add(enemy);
        }

        private void OnRoomTick(SimulationRoom tickedRoom, TimeSpan delta, DateTime now)
        {
            if (room == null || tickedRoom != room || logger == null)
            {
                return;
            }

            var nextTick = Interlocked.Increment(ref tickIndex);
            var outputs = room.ConsumeOutputs();
            _ = DispatchOutputsAsync(outputs, nextTick);
        }

        private async Task DispatchOutputsAsync(IReadOnlyCollection<SimulationOutput> outputs, long tick)
        {
            foreach (var output in outputs)
            {
                switch (output)
                {
                    case MovementOutput movement:
                        logger?.AppendEvent(DungeonRunEventTypes.MovementOutput, new MovementLogEntry(movement.ActorId, movement.Command, tick));
                        await SendToPlayerAsync(movement.ActorId, new MessageEnvelope<MovementCommand>
                        {
                            Type = MessageTypes.MovementState,
                            SessionId = GetSessionId(movement.ActorId),
                            Payload = movement.Command
                        }).ConfigureAwait(false);
                        break;
                    case AbilityCastOutput cast:
                        logger?.AppendEvent(DungeonRunEventTypes.AbilityOutput, new AbilityCastResultLogEntry(cast.ActorId, cast.Result, tick));
                        await SendToPlayerAsync(cast.ActorId, new MessageEnvelope<AbilityCastResult>
                        {
                            Type = MessageTypes.AbilityCastResult,
                            SessionId = GetSessionId(cast.ActorId),
                            RequestId = cast.RequestId ?? string.Empty,
                            Payload = cast.Result
                        }).ConfigureAwait(false);
                        break;
                    case CombatOutput combat:
                        logger?.AppendEvent(DungeonRunEventTypes.CombatOutput, new CombatLogEntry(combat.Event, tick));
                        await BroadcastAsync(new MessageEnvelope<CombatEvent>
                        {
                            Type = MessageTypes.CombatEvent,
                            Payload = combat.Event
                        }).ConfigureAwait(false);
                        break;
                }
            }

            CheckRoomClear(tick);
        }

        private void CheckRoomClear(long tick)
        {
            if (validator == null || roomCleared || string.IsNullOrWhiteSpace(activeRoomId))
            {
                return;
            }

            if (enemyActors.All(enemy => enemy.Resources.Health <= 0))
            {
                var result = validator.MarkRoomCleared(activeRoomId);
                if (result.Accepted)
                {
                    roomCleared = true;
                    logger?.AppendEvent(DungeonRunEventTypes.RoomCleared, new RoomClearedLogEntry(activeRoomId, tick));
                }
            }
        }

        private async Task BroadcastAsync<TPayload>(MessageEnvelope<TPayload> envelope) where TPayload : class
        {
            foreach (var connection in connections.Values)
            {
                await connection.Sender.SendAsync(CloneEnvelope(envelope, connection.SessionId)).ConfigureAwait(false);
            }
        }

        private async Task SendToPlayerAsync<TPayload>(string playerId, MessageEnvelope<TPayload> envelope) where TPayload : class
        {
            if (!connections.TryGetValue(playerId, out var connection))
            {
                return;
            }

            await connection.Sender.SendAsync(CloneEnvelope(envelope, connection.SessionId)).ConfigureAwait(false);
        }

        private static MessageEnvelope<TPayload> CloneEnvelope<TPayload>(MessageEnvelope<TPayload> envelope, string sessionId)
            where TPayload : class
        {
            return new MessageEnvelope<TPayload>
            {
                Type = envelope.Type,
                SessionId = sessionId,
                RequestId = envelope.RequestId,
                Payload = envelope.Payload
            };
        }

        private string GetSessionId(string playerId)
        {
            return connections.TryGetValue(playerId, out var connection) ? connection.SessionId : string.Empty;
        }

        private async Task SendLayoutIfNeededAsync(string playerId)
        {
            if (layoutSummary == null || room == null || !layoutSent.TryAdd(playerId, 0))
            {
                return;
            }

            await SendToPlayerAsync(playerId, new MessageEnvelope<DungeonLayoutSummary>
            {
                Type = MessageTypes.DungeonLayout,
                Payload = layoutSummary
            }).ConfigureAwait(false);
        }

        private static bool RequiresEnemyClear(RoomArchetype archetype)
        {
            return archetype == RoomArchetype.Enemy
                || archetype == RoomArchetype.MiniBoss
                || archetype == RoomArchetype.Boss
                || archetype == RoomArchetype.Trap;
        }

        private record PlayerConnection(IMessageSender Sender, string SessionId);
    }

    public static class DungeonTemplateDefaults
    {
        public static DungeonTemplate CreateDefault(string dungeonId)
        {
            return new DungeonTemplate
            {
                DungeonId = dungeonId,
                Rooms = new List<RoomTemplate>
                {
                    new RoomTemplate
                    {
                        TemplateId = "default-room",
                        Features = RoomFeature.None,
                        SpawnPoints = new List<SpawnPoint>
                        {
                            new SpawnPoint("player", new Vector3(1, 0, 1), "player")
                        },
                        Doors = new List<DoorTemplate>
                        {
                            new DoorTemplate
                            {
                                DoorId = "default-door",
                                SocketId = "exit",
                                StartsLocked = false,
                                ConfigId = "default"
                            }
                        }
                    }
                },
                DoorConfigs = new List<DoorConfig>
                {
                    new DoorConfig
                    {
                        ConfigId = "default",
                        SupportsLockedState = true,
                        SupportsSealedState = true
                    }
                }
            };
        }
    }
}
