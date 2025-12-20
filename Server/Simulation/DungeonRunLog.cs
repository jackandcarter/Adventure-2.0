using System.Collections.Generic;
using System.Text.Json;
using Adventure.Server.Core.Repositories;
using Adventure.Server.Persistence;
using Adventure.Shared.Network.Messages;

namespace Adventure.Server.Simulation
{
    public static class DungeonRunEventTypes
    {
        public const string Seed = "seed";
        public const string MovementInput = "input.movement";
        public const string AbilityInput = "input.ability";
        public const string MovementOutput = "output.movement";
        public const string AbilityOutput = "output.ability";
        public const string CombatOutput = "output.combat";
        public const string RoomCleared = "room.cleared";
    }

    public record SeedLogEntry(string DungeonId, int Seed);

    public record MovementLogEntry(string PlayerId, MovementCommand Command, long Tick);

    public record AbilityCastLogEntry(string PlayerId, AbilityCastRequest Request, long Tick);

    public record AbilityCastResultLogEntry(string PlayerId, AbilityCastResult Result, long Tick);

    public record CombatLogEntry(CombatEvent Event, long Tick);

    public record RoomClearedLogEntry(string RoomId, long Tick);

    public class DungeonRunLogger
    {
        private readonly IDungeonRunRepository repository;
        private readonly string runId;
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public DungeonRunLogger(IDungeonRunRepository repository, string runId)
        {
            this.repository = repository;
            this.runId = runId;
        }

        public void AppendEvent(string eventType, object payload)
        {
            var json = JsonSerializer.Serialize(payload, serializerOptions);
            repository.AppendEvent(new RunEventRecord(0, runId, eventType, json, System.DateTime.UtcNow));
        }
    }

    public class DungeonRunReplay
    {
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public DungeonRunRecord Run { get; }
        public IReadOnlyCollection<RunEventRecord> Events { get; }

        public DungeonRunReplay(DungeonRunRecord run, IReadOnlyCollection<RunEventRecord> events)
        {
            Run = run;
            Events = events;
        }

        public SeedLogEntry? GetSeed()
        {
            return DeserializeSingle<SeedLogEntry>(DungeonRunEventTypes.Seed);
        }

        public IEnumerable<MovementLogEntry> MovementInputs() => DeserializeMany<MovementLogEntry>(DungeonRunEventTypes.MovementInput);

        public IEnumerable<AbilityCastLogEntry> AbilityInputs() => DeserializeMany<AbilityCastLogEntry>(DungeonRunEventTypes.AbilityInput);

        public IEnumerable<CombatLogEntry> CombatOutputs() => DeserializeMany<CombatLogEntry>(DungeonRunEventTypes.CombatOutput);

        private T? DeserializeSingle<T>(string eventType)
        {
            foreach (var logEvent in Events)
            {
                if (logEvent.EventType == eventType)
                {
                    return JsonSerializer.Deserialize<T>(logEvent.PayloadJson, serializerOptions);
                }
            }

            return default;
        }

        private IEnumerable<T> DeserializeMany<T>(string eventType)
        {
            foreach (var logEvent in Events)
            {
                if (logEvent.EventType == eventType)
                {
                    var entry = JsonSerializer.Deserialize<T>(logEvent.PayloadJson, serializerOptions);
                    if (entry != null)
                    {
                        yield return entry;
                    }
                }
            }
        }
    }
}
