using System;
using System.Collections.Generic;
using System.Linq;
using Adventure.Server.Network;
using Adventure.Shared.Network.Messages;

namespace Adventure.Server.Generation
{
    public class DungeonStateValidator
    {
        private readonly GeneratedDungeon dungeon;
        private readonly Dictionary<string, GeneratedDoor> doorsById;
        private readonly Dictionary<string, GeneratedInteractive> interactivesById;
        private readonly Dictionary<string, TriggerTemplate> triggersById;
        private readonly Dictionary<string, GeneratedEnvironmentState> environmentStates;
        private readonly Dictionary<string, RoomArchetype> roomArchetypes;
        private readonly Dictionary<string, bool> roomClearState;
        private readonly Dictionary<string, Dictionary<string, KeyTag?>> playerKeyring = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> activatedTriggers = new(StringComparer.OrdinalIgnoreCase);

        public DungeonStateValidator(GeneratedDungeon dungeon)
        {
            this.dungeon = dungeon;
            doorsById = dungeon.Doors.ToDictionary(d => d.DoorId, StringComparer.OrdinalIgnoreCase);
            interactivesById = dungeon.InteractiveObjects.ToDictionary(i => i.ObjectId, StringComparer.OrdinalIgnoreCase);
            triggersById = dungeon.Rooms
                .SelectMany(r => r.Template.Triggers)
                .ToDictionary(t => t.TriggerId, StringComparer.OrdinalIgnoreCase);
            environmentStates = dungeon.EnvironmentStates
                .ToDictionary(e => $"{e.RoomId}:{e.StateId}", StringComparer.OrdinalIgnoreCase);
            roomArchetypes = dungeon.Rooms.ToDictionary(r => r.RoomId, r => r.Archetype, StringComparer.OrdinalIgnoreCase);
            roomClearState = dungeon.Rooms.ToDictionary(
                r => r.RoomId,
                r => !RequiresEnemyClear(r.Archetype),
                StringComparer.OrdinalIgnoreCase);
        }

        public DungeonLayoutSummary CreateLayoutSummary()
        {
            var templateLookup = new Dictionary<string, RoomTemplateSummary>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in dungeon.Rooms)
            {
                if (!templateLookup.ContainsKey(room.Template.TemplateId))
                {
                    templateLookup[room.Template.TemplateId] = ToRoomTemplateSummary(room.Template);
                }
            }

            return new DungeonLayoutSummary
            {
                DungeonId = dungeon.DungeonId,
                Rooms = dungeon.Rooms.Select(r => new DungeonRoomSummary
                {
                    RoomId = r.RoomId,
                    TemplateId = r.Template.TemplateId,
                    Archetype = r.Archetype,
                    Features = r.Features,
                    SequenceIndex = r.SequenceIndex
                }).ToList(),
                Doors = dungeon.Doors.Select(ToDoorState).ToList(),
                Interactives = dungeon.InteractiveObjects.Select(ToInteractiveState).ToList(),
                EnvironmentStates = dungeon.EnvironmentStates.Select(ToEnvironmentState).ToList(),
                RoomTemplates = templateLookup.Values.ToList()
            };
        }

        public List<string> ValidateStaticLayout()
        {
            var errors = new List<string>();
            ValidateDoorConfigs(errors);
            ValidateStairs(errors);
            ValidateLockedDoorKeys(errors);
            return errors;
        }

        public DungeonActionResult RegisterKeyPickup(string playerId, string interactiveId)
        {
            if (!interactivesById.TryGetValue(interactiveId, out var interactive))
            {
                return DungeonActionResult.Denied("unknown_interactive");
            }

            if (interactive.Status == InteractiveStatus.Consumed)
            {
                return DungeonActionResult.Denied("already_consumed");
            }

            if (string.IsNullOrWhiteSpace(interactive.GrantsKeyId))
            {
                return DungeonActionResult.Denied("no_key_available");
            }

            AddKeyToPlayer(playerId, interactive.GrantsKeyId, interactive.GrantsKeyTag);
            interactive.Status = InteractiveStatus.Consumed;

            return DungeonActionResult.Accepted(new DungeonStateDelta
            {
                Interactives = new List<InteractiveObjectState> { ToInteractiveState(interactive) }
            });
        }

        public DungeonActionResult TryOpenDoor(string playerId, string doorId, string? providedKeyId)
        {
            if (!doorsById.TryGetValue(doorId, out var door))
            {
                return DungeonActionResult.Denied("unknown_door");
            }

            if (roomClearState.TryGetValue(door.FromRoomId, out var cleared) && !cleared && RequiresEnemyClear(roomArchetypes[door.FromRoomId]))
            {
                return DungeonActionResult.Denied("room_uncleared");
            }

            if (door.State == DoorState.Open)
            {
                return DungeonActionResult.Accepted(null);
            }

            if (door.State == DoorState.Sealed)
            {
                return DungeonActionResult.Denied("sealed_door");
            }

            if (!string.IsNullOrWhiteSpace(door.RequiredKeyId) || door.RequiredKeyTag.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(door.RequiredKeyId)
                    && (string.IsNullOrWhiteSpace(providedKeyId) || !string.Equals(door.RequiredKeyId, providedKeyId, StringComparison.OrdinalIgnoreCase)))
                {
                    return DungeonActionResult.Denied("key_required");
                }

                if (!TryConsumeKey(playerId, providedKeyId, door.RequiredKeyTag, out var denial))
                {
                    return DungeonActionResult.Denied(denial);
                }
            }

            door.State = DoorState.Open;

            return DungeonActionResult.Accepted(new DungeonStateDelta
            {
                Doors = new List<DungeonDoorState> { ToDoorState(door) }
            });
        }

        public DungeonActionResult TryActivateTrigger(string playerId, string triggerId)
        {
            if (!triggersById.TryGetValue(triggerId, out var triggerTemplate))
            {
                return DungeonActionResult.Denied("unknown_trigger");
            }

            if (activatedTriggers.Contains(triggerId))
            {
                return DungeonActionResult.Accepted(null);
            }

            if (triggerTemplate.RequiredTriggers.Any(required => !activatedTriggers.Contains(required)))
            {
                return DungeonActionResult.Denied("prerequisite_missing");
            }

            activatedTriggers.Add(triggerId);

            var delta = new DungeonStateDelta();
            if (!string.IsNullOrWhiteSpace(triggerTemplate.ActivatesStateId))
            {
                var envKey = environmentStates.Keys.FirstOrDefault(k => k.EndsWith($":{triggerTemplate.ActivatesStateId}", StringComparison.OrdinalIgnoreCase));
                if (envKey != null)
                {
                    var state = environmentStates[envKey];
                    state.Value = "active";
                    delta.EnvironmentStates.Add(ToEnvironmentState(state));
                }
            }

            return DungeonActionResult.Accepted(delta.EnvironmentStates.Count == 0 ? null : delta);
        }

        public DungeonActionResult UseInteractive(string playerId, string interactiveId, string? providedKeyId)
        {
            if (!interactivesById.TryGetValue(interactiveId, out var interactive))
            {
                return DungeonActionResult.Denied("unknown_interactive");
            }

            if (interactive.Status == InteractiveStatus.Consumed)
            {
                return DungeonActionResult.Denied("already_consumed");
            }

            if (!string.IsNullOrWhiteSpace(interactive.RequiresKeyId) || interactive.RequiresKeyTag.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(interactive.RequiresKeyId)
                    && (string.IsNullOrWhiteSpace(providedKeyId)
                        || !string.Equals(interactive.RequiresKeyId, providedKeyId, StringComparison.OrdinalIgnoreCase)))
                {
                    return DungeonActionResult.Denied("key_required");
                }

                if (!TryConsumeKey(playerId, providedKeyId, interactive.RequiresKeyTag, out var denial))
                {
                    return DungeonActionResult.Denied(denial);
                }
            }

            interactive.Status = InteractiveStatus.Consumed;
            var delta = new DungeonStateDelta
            {
                Interactives = new List<InteractiveObjectState> { ToInteractiveState(interactive) }
            };

            if (!string.IsNullOrWhiteSpace(interactive.GrantsKeyId))
            {
                AddKeyToPlayer(playerId, interactive.GrantsKeyId, interactive.GrantsKeyTag);
            }

            if (!string.IsNullOrWhiteSpace(interactive.ActivatesTriggerId))
            {
                var triggerResult = TryActivateTrigger(playerId, interactive.ActivatesTriggerId);
                if (!triggerResult.Accepted)
                {
                    return triggerResult;
                }

                if (triggerResult.Delta != null)
                {
                    delta.EnvironmentStates.AddRange(triggerResult.Delta.EnvironmentStates);
                }
            }

            return DungeonActionResult.Accepted(delta);
        }

        public DungeonActionResult MarkRoomCleared(string roomId)
        {
            if (!roomArchetypes.TryGetValue(roomId, out var roomType))
            {
                return DungeonActionResult.Denied("unknown_room");
            }

            if (!RequiresEnemyClear(roomType))
            {
                return DungeonActionResult.Accepted(null);
            }

            roomClearState[roomId] = true;
            return DungeonActionResult.Accepted(null);
        }

        private bool TryConsumeKey(string playerId, string? keyId, KeyTag? requiredTag, out string denial)
        {
            denial = "missing_key";
            if (!playerKeyring.TryGetValue(playerId, out var keys) || keys.Count == 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(keyId))
            {
                if (!keys.TryGetValue(keyId, out var keyTag))
                {
                    return false;
                }

                if (requiredTag.HasValue && keyTag != requiredTag)
                {
                    denial = "key_required";
                    return false;
                }

                keys.Remove(keyId);
                return true;
            }

            if (requiredTag.HasValue)
            {
                var match = keys.FirstOrDefault(pair => pair.Value == requiredTag);
                if (!string.IsNullOrWhiteSpace(match.Key))
                {
                    keys.Remove(match.Key);
                    return true;
                }
            }

            return false;
        }

        private void AddKeyToPlayer(string playerId, string keyId, KeyTag? keyTag)
        {
            if (!playerKeyring.TryGetValue(playerId, out var keys))
            {
                keys = new Dictionary<string, KeyTag?>(StringComparer.OrdinalIgnoreCase);
                playerKeyring[playerId] = keys;
            }

            keys[keyId] = keyTag;
        }
            }
        }

        private static DungeonDoorState ToDoorState(GeneratedDoor door)
        {
            return new DungeonDoorState
            {
                DoorId = door.DoorId,
                FromRoomId = door.FromRoomId,
                ToRoomId = door.ToRoomId,
                RequiredKeyId = door.RequiredKeyId,
                RequiredKeyTag = door.RequiredKeyTag,
                State = door.State
            };
        }

        private static InteractiveObjectState ToInteractiveState(GeneratedInteractive interactive)
        {
            return new InteractiveObjectState
            {
                ObjectId = interactive.ObjectId,
                RoomId = interactive.RoomId,
                Kind = interactive.Kind,
                GrantedKeyId = interactive.GrantsKeyId,
                GrantedKeyTag = interactive.GrantsKeyTag,
                Status = interactive.Status
            };
        }

        private static RoomTemplateSummary ToRoomTemplateSummary(RoomTemplate template)
        {
            return new RoomTemplateSummary
            {
                TemplateId = template.TemplateId,
                DisplayName = template.TemplateId,
                RoomType = RoomTemplateType.Enemy,
                Doors = template.Doors.Select(door => new DoorTemplateSummary
                {
                    DoorId = door.DoorId,
                    SocketId = door.SocketId,
                    RequiredKeyId = door.RequiredKeyId,
                    RequiredKeyTag = door.RequiredKeyTag,
                    StartsLocked = door.StartsLocked,
                    IsOneWay = door.IsOneWay
                }).ToList(),
                Triggers = template.Triggers.Select(trigger => new TriggerTemplateSummary
                {
                    TriggerId = trigger.TriggerId,
                    RequiredTriggers = trigger.RequiredTriggers.ToList(),
                    ActivatesStateId = trigger.ActivatesStateId,
                    ServerOnly = trigger.ServerOnly
                }).ToList(),
                InteractiveObjects = template.InteractiveObjects.Select(interactive => new InteractiveTemplateSummary
                {
                    ObjectId = interactive.ObjectId,
                    Kind = interactive.Kind,
                    GrantsKeyId = interactive.GrantsKeyId,
                    GrantsKeyTag = interactive.GrantsKeyTag,
                    RequiresKeyId = interactive.RequiresKeyId,
                    RequiresKeyTag = interactive.RequiresKeyTag,
                    ActivatesTriggerId = interactive.ActivatesTriggerId
                }).ToList(),
                ProvidesKeys = template.ProvidesKeys.ToList(),
                EnvironmentStates = template.EnvironmentStates.Select(state => new EnvironmentStateDefinitionSnapshot
                {
                    StateId = state.StateId,
                    DefaultValue = state.DefaultValue
                }).ToList()
            };
        }

        private static EnvironmentStateSnapshot ToEnvironmentState(GeneratedEnvironmentState state)
        {
            return new EnvironmentStateSnapshot
            {
                RoomId = state.RoomId,
                StateId = state.StateId,
                Value = state.Value
            };
        }

        private void ValidateDoorConfigs(List<string> errors)
        {
            foreach (var door in dungeon.Doors)
            {
                if (!dungeon.DoorConfigs.TryGetValue(door.ConfigId, out var config))
                {
                    errors.Add($"Door {door.DoorId} references missing config '{door.ConfigId}'.");
                    continue;
                }

                if (door.State == DoorState.Locked && !config.SupportsLockedState)
                {
                    errors.Add($"Door {door.DoorId} requires lock state, but '{door.ConfigId}' does not support it.");
                }

                if (door.State == DoorState.Sealed && !config.SupportsSealedState)
                {
                    errors.Add($"Door {door.DoorId} is sealed, but '{door.ConfigId}' does not support sealing.");
                }
            }
        }

        private void ValidateStairs(List<string> errors)
        {
            var stairSockets = dungeon.Rooms
                .SelectMany(room => room.Template.Stairs.Select(socket => (room, socket)))
                .ToList();

            var stairsUp = stairSockets.Where(s => s.socket.FloorOffset > 0).ToList();
            var stairsDown = stairSockets.Where(s => s.socket.FloorOffset < 0).ToList();

            foreach (var up in stairsUp)
            {
                var expectedFloor = up.room.SequenceIndex + up.socket.FloorOffset;
                var matching = stairsDown.Any(down =>
                    down.room.SequenceIndex == expectedFloor &&
                    down.socket.TargetGridX == up.socket.TargetGridX &&
                    down.socket.TargetGridY == up.socket.TargetGridY &&
                    string.Equals(down.socket.Facing, up.socket.Facing, StringComparison.OrdinalIgnoreCase));

                if (!matching)
                {
                    errors.Add($"Stair socket {up.socket.SocketId} in room {up.room.RoomId} is missing a paired descent.");
                }
            }
        }

        private void ValidateLockedDoorKeys(List<string> errors)
        {
            foreach (var door in dungeon.Doors.Where(d => d.State == DoorState.Locked && !string.IsNullOrWhiteSpace(d.RequiredKeyId)))
            {
                var sourceRooms = dungeon.Rooms.Where(r => r.SequenceIndex <= GetRoomSequence(door.FromRoomId)).ToList();
                var hasSource = sourceRooms.Any(room => HasKeySource(room, door.RequiredKeyId!, door.RequiredKeyTag));

                if (!hasSource)
                {
                    errors.Add($"Locked door {door.DoorId} does not have an upstream key source for '{door.RequiredKeyId}'.");
                }
            }
        }

        private static bool HasKeySource(GeneratedRoom room, string requiredKeyId, KeyTag? keyTag)
        {
            if (room.Template.ProvidesKeys.Any(k => string.Equals(k, requiredKeyId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return room.InteractiveObjects.Any(interactive =>
                string.Equals(interactive.GrantsKeyId, requiredKeyId, StringComparison.OrdinalIgnoreCase)
                || (keyTag.HasValue && interactive.GrantsKeyTag == keyTag));
        }

        private int GetRoomSequence(string roomId)
        {
            var room = dungeon.Rooms.FirstOrDefault(r => r.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase));
            return room?.SequenceIndex ?? 0;
        }

        private static bool RequiresEnemyClear(RoomArchetype archetype)
        {
            return archetype == RoomArchetype.Enemy
                || archetype == RoomArchetype.MiniBoss
                || archetype == RoomArchetype.Boss
                || archetype == RoomArchetype.Trap;
        }
    }

    public class DungeonActionResult
    {
        public bool Accepted { get; init; }

        public string? Reason { get; init; }

        public DungeonStateDelta? Delta { get; init; }

        public static DungeonActionResult Denied(string reason)
        {
            return new DungeonActionResult { Accepted = false, Reason = reason };
        }

        public static DungeonActionResult Accepted(DungeonStateDelta? delta)
        {
            return new DungeonActionResult { Accepted = true, Delta = delta };
        }
    }

    public class DungeonStateBroadcaster
    {
        private readonly IMessageSender sender;

        public DungeonStateBroadcaster(IMessageSender sender)
        {
            this.sender = sender;
        }

        public System.Threading.Tasks.Task SendLayoutAsync(DungeonLayoutSummary layout)
        {
            return sender.SendAsync(new MessageEnvelope<DungeonLayoutSummary>
            {
                Type = MessageTypes.DungeonLayout,
                Payload = layout
            });
        }

        public System.Threading.Tasks.Task SendDeltaAsync(DungeonStateDelta delta)
        {
            return sender.SendAsync(new MessageEnvelope<DungeonStateDelta>
            {
                Type = MessageTypes.DungeonStateDelta,
                Payload = delta
            });
        }
    }
}
