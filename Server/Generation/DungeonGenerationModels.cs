using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Adventure.Shared.Network.Messages;

namespace Adventure.Server.Generation
{
    public class DungeonTemplate
    {
        public string DungeonId { get; set; } = string.Empty;

        public List<RoomTemplate> Rooms { get; set; } = new();
    }

    public class RoomTemplate
    {
        public string TemplateId { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RoomTemplateType RoomType { get; set; }

        public List<SpawnPoint> SpawnPoints { get; set; } = new();

        public List<DoorTemplate> Doors { get; set; } = new();

        public List<TriggerTemplate> Triggers { get; set; } = new();

        public List<InteractiveObjectTemplate> InteractiveObjects { get; set; } = new();

        /// <summary>
        /// Keys the room guarantees can be obtained (often by solving its objective or opening a chest).
        /// </summary>
        public List<string> ProvidesKeys { get; set; } = new();

        public List<EnvironmentStateDefinition> EnvironmentStates { get; set; } = new();
    }

    public record SpawnPoint(string Id, Vector3 Position, string Tag);

    public class DoorTemplate
    {
        public string DoorId { get; set; } = string.Empty;

        public string SocketId { get; set; } = string.Empty;

        public string? RequiredKeyId { get; set; }

        public bool StartsLocked { get; set; }

        public bool IsOneWay { get; set; }
    }

    public class TriggerTemplate
    {
        public string TriggerId { get; set; } = string.Empty;

        public List<string> RequiredTriggers { get; set; } = new();

        public string? ActivatesStateId { get; set; }

        public bool ServerOnly { get; set; }
    }

    public class InteractiveObjectTemplate
    {
        public string ObjectId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string? GrantsKeyId { get; set; }

        public string? RequiresKeyId { get; set; }

        public string? ActivatesTriggerId { get; set; }
    }

    public class EnvironmentStateDefinition
    {
        public string StateId { get; set; } = string.Empty;

        public string DefaultValue { get; set; } = string.Empty;
    }

    public class DungeonGenerationSettings
    {
        public int EnemyRooms { get; set; } = 2;

        public int TreasureRooms { get; set; } = 1;

        public bool IncludeMiniboss { get; set; } = true;

        public bool IncludeSecretStaircase { get; set; } = true;

        public int Seed { get; set; } = Environment.TickCount;
    }

    public class GeneratedDungeon
    {
        public string DungeonId { get; }

        public IReadOnlyList<GeneratedRoom> Rooms => rooms;

        public IReadOnlyList<GeneratedDoor> Doors => doors;

        public IReadOnlyList<GeneratedInteractive> InteractiveObjects => interactives;

        public IReadOnlyList<GeneratedEnvironmentState> EnvironmentStates => environmentStates;

        private readonly List<GeneratedRoom> rooms;
        private readonly List<GeneratedDoor> doors;
        private readonly List<GeneratedInteractive> interactives;
        private readonly List<GeneratedEnvironmentState> environmentStates;

        public GeneratedDungeon(
            string dungeonId,
            List<GeneratedRoom> rooms,
            List<GeneratedDoor> doors,
            List<GeneratedInteractive> interactives,
            List<GeneratedEnvironmentState> environmentStates)
        {
            DungeonId = dungeonId;
            this.rooms = rooms;
            this.doors = doors;
            this.interactives = interactives;
            this.environmentStates = environmentStates;
        }
    }

    public class GeneratedRoom
    {
        public string RoomId { get; }

        public RoomTemplate Template { get; }

        public int SequenceIndex { get; }

        public List<GeneratedDoor> Doors { get; } = new();

        public List<GeneratedInteractive> InteractiveObjects { get; } = new();

        public List<GeneratedEnvironmentState> EnvironmentStates { get; } = new();

        public GeneratedRoom(string roomId, RoomTemplate template, int sequenceIndex)
        {
            RoomId = roomId;
            Template = template;
            SequenceIndex = sequenceIndex;
        }
    }

    public class GeneratedDoor
    {
        public string DoorId { get; set; } = string.Empty;

        public string FromRoomId { get; set; } = string.Empty;

        public string ToRoomId { get; set; } = string.Empty;

        public string? RequiredKeyId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DoorState State { get; set; } = DoorState.Closed;
    }

    public class GeneratedInteractive
    {
        public string ObjectId { get; set; } = string.Empty;

        public string RoomId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string? GrantsKeyId { get; set; }

        public string? RequiresKeyId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public InteractiveStatus Status { get; set; } = InteractiveStatus.Available;

        public string? ActivatesTriggerId { get; set; }
    }

    public class GeneratedEnvironmentState
    {
        public string RoomId { get; set; } = string.Empty;

        public string StateId { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }

    public static class DungeonTemplateLoader
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static DungeonTemplate LoadFromJson(string json)
        {
            return JsonSerializer.Deserialize<DungeonTemplate>(json, Options)
                   ?? throw new InvalidOperationException("Failed to deserialize dungeon template from JSON.");
        }

        public static DungeonTemplate LoadFromStream(Stream stream)
        {
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return LoadFromJson(json);
        }
    }
}
