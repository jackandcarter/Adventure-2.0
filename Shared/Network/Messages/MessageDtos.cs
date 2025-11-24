using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Adventure.Shared.Network.Messages
{
    /// <summary>
    /// Envelope describing the payload and who sent it so routers can apply validation.
    /// </summary>
    public class MessageEnvelope
    {
        public string Type { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;

        public object? Payload { get; set; }
    }

    /// <summary>
    /// Generic envelope variant that keeps payload strongly typed after deserialization.
    /// </summary>
    /// <typeparam name="TPayload">Payload contract.</typeparam>
    public class MessageEnvelope<TPayload> : MessageEnvelope where TPayload : class
    {
        public new TPayload? Payload { get; set; }
    }

    public static class MessageTypes
    {
        public const string AuthRequest = "auth/request";
        public const string AuthResponse = "auth/response";
        public const string LobbyJoin = "lobby/join";
        public const string LobbyUpdate = "lobby/update";
        public const string ChatSend = "chat/send";
        public const string ChatBroadcast = "chat/broadcast";
        public const string PartyUpdate = "party/update";
        public const string Movement = "movement/input";
        public const string AbilityCast = "ability/cast";
        public const string CombatEvent = "combat/event";
        public const string DungeonState = "dungeon/state";
        public const string DungeonLayout = "dungeon/layout";
        public const string DungeonStateDelta = "dungeon/update";
        public const string Heartbeat = "system/heartbeat";
        public const string Error = "system/error";
    }

    // Auth
    public class AuthRequest
    {
        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string ClientVersion { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public bool Success { get; set; }

        public string SessionId { get; set; } = string.Empty;

        public string PlayerId { get; set; } = string.Empty;

        public string DenialReason { get; set; } = string.Empty;
    }

    // Lobby
    public class LobbyJoinRequest
    {
        public string LobbyId { get; set; } = string.Empty;
    }

    public class LobbySnapshot
    {
        public string LobbyId { get; set; } = string.Empty;

        public List<LobbyMember> Members { get; set; } = new();

        public string Status { get; set; } = string.Empty;
    }

    public class LobbyMember
    {
        public string PlayerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsReady { get; set; }
    }

    // Chat
    public class ChatSendRequest
    {
        public string Channel { get; set; } = "global";

        public string Message { get; set; } = string.Empty;
    }

    public class ChatMessage
    {
        public string Channel { get; set; } = "global";

        public string Sender { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // Party
    public class PartyState
    {
        public List<PartyMember> Members { get; set; } = new();
    }

    public class PartyMember
    {
        public string PlayerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int Level { get; set; }

        public float HealthFraction { get; set; }

        public bool Leader { get; set; }
    }

    // Movement
    public class MovementCommand
    {
        public Vector3 Position { get; set; }

        public Vector3 Direction { get; set; }

        public float Speed { get; set; }

        public bool IsSprinting { get; set; }
    }

    // Ability cast
    public class AbilityCastRequest
    {
        public string AbilityId { get; set; } = string.Empty;

        public Vector3 TargetPosition { get; set; }

        public string TargetId { get; set; } = string.Empty;
    }

    public class AbilityCastResult
    {
        public string AbilityId { get; set; } = string.Empty;

        public bool Accepted { get; set; }

        public string DenialReason { get; set; } = string.Empty;
    }

    // Damage/Heal
    public class CombatEvent
    {
        public string SourceId { get; set; } = string.Empty;

        public string TargetId { get; set; } = string.Empty;

        public int Amount { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CombatEventType EventType { get; set; } = CombatEventType.Damage;

        public string AbilityId { get; set; } = string.Empty;
    }

    public enum CombatEventType
    {
        Damage,
        Heal
    }

    public enum RoomTemplateType
    {
        Start,
        Safe,
        Enemy,
        Illusion,
        Treasure,
        Miniboss,
        Boss,
        StaircaseUp,
        StaircaseDown
    }

    public enum DoorState
    {
        Closed,
        Locked,
        Open
    }

    public enum InteractiveStatus
    {
        Available,
        Consumed
    }

    // Dungeon state
    public class DungeonState
    {
        public string DungeonId { get; set; } = string.Empty;

        public string Phase { get; set; } = string.Empty;

        public List<string> CompletedObjectives { get; set; } = new();

        public List<string> ActiveObjectives { get; set; } = new();
    }

    public class DungeonLayoutSummary
    {
        public string DungeonId { get; set; } = string.Empty;

        public List<DungeonRoomSummary> Rooms { get; set; } = new();

        public List<DungeonDoorState> Doors { get; set; } = new();

        public List<InteractiveObjectState> Interactives { get; set; } = new();

        public List<EnvironmentStateSnapshot> EnvironmentStates { get; set; } = new();
    }

    public class DungeonRoomSummary
    {
        public string RoomId { get; set; } = string.Empty;

        public string TemplateId { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RoomTemplateType RoomType { get; set; }

        public int SequenceIndex { get; set; }

        public int GridX { get; set; }

        public int GridY { get; set; }
    }

    public class DungeonDoorState
    {
        public string DoorId { get; set; } = string.Empty;

        public string FromRoomId { get; set; } = string.Empty;

        public string ToRoomId { get; set; } = string.Empty;

        public string? RequiredKeyId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DoorState State { get; set; }
    }

    public class InteractiveObjectState
    {
        public string ObjectId { get; set; } = string.Empty;

        public string RoomId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string? GrantedKeyId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public InteractiveStatus Status { get; set; }
    }

    public class EnvironmentStateSnapshot
    {
        public string RoomId { get; set; } = string.Empty;

        public string StateId { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }

    public class DungeonStateDelta
    {
        public List<DungeonDoorState> Doors { get; set; } = new();

        public List<InteractiveObjectState> Interactives { get; set; } = new();

        public List<EnvironmentStateSnapshot> EnvironmentStates { get; set; } = new();
    }

    // System / errors
    public class Heartbeat
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ErrorResponse
    {
        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public int RetryAfterSeconds { get; set; }
    }
}
