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
        private readonly Dictionary<string, HashSet<string>> playerKeyring = new(StringComparer.OrdinalIgnoreCase);
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
        }

        public DungeonLayoutSummary CreateLayoutSummary()
        {
            return new DungeonLayoutSummary
            {
                DungeonId = dungeon.DungeonId,
                Rooms = dungeon.Rooms.Select(r => new DungeonRoomSummary
                {
                    RoomId = r.RoomId,
                    TemplateId = r.Template.TemplateId,
                    RoomType = r.Template.RoomType,
                    SequenceIndex = r.SequenceIndex
                }).ToList(),
                Doors = dungeon.Doors.Select(ToDoorState).ToList(),
                Interactives = dungeon.InteractiveObjects.Select(ToInteractiveState).ToList(),
                EnvironmentStates = dungeon.EnvironmentStates.Select(ToEnvironmentState).ToList()
            };
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

            AddKeyToPlayer(playerId, interactive.GrantsKeyId);
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

            if (door.State == DoorState.Open)
            {
                return DungeonActionResult.Accepted(null);
            }

            if (!string.IsNullOrWhiteSpace(door.RequiredKeyId))
            {
                if (string.IsNullOrWhiteSpace(providedKeyId) || !string.Equals(door.RequiredKeyId, providedKeyId, StringComparison.OrdinalIgnoreCase))
                {
                    return DungeonActionResult.Denied("key_required");
                }

                if (!PlayerHasKey(playerId, providedKeyId))
                {
                    return DungeonActionResult.Denied("missing_key");
                }

                RemoveKeyFromPlayer(playerId, providedKeyId);
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

            if (!string.IsNullOrWhiteSpace(interactive.RequiresKeyId))
            {
                if (string.IsNullOrWhiteSpace(providedKeyId) || !string.Equals(interactive.RequiresKeyId, providedKeyId, StringComparison.OrdinalIgnoreCase))
                {
                    return DungeonActionResult.Denied("key_required");
                }

                if (!PlayerHasKey(playerId, providedKeyId))
                {
                    return DungeonActionResult.Denied("missing_key");
                }

                RemoveKeyFromPlayer(playerId, providedKeyId);
            }

            interactive.Status = InteractiveStatus.Consumed;
            var delta = new DungeonStateDelta
            {
                Interactives = new List<InteractiveObjectState> { ToInteractiveState(interactive) }
            };

            if (!string.IsNullOrWhiteSpace(interactive.GrantsKeyId))
            {
                AddKeyToPlayer(playerId, interactive.GrantsKeyId);
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

        private bool PlayerHasKey(string playerId, string keyId)
        {
            return playerKeyring.TryGetValue(playerId, out var keys) && keys.Contains(keyId);
        }

        private void AddKeyToPlayer(string playerId, string keyId)
        {
            if (!playerKeyring.TryGetValue(playerId, out var keys))
            {
                keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                playerKeyring[playerId] = keys;
            }

            keys.Add(keyId);
        }

        private void RemoveKeyFromPlayer(string playerId, string keyId)
        {
            if (playerKeyring.TryGetValue(playerId, out var keys))
            {
                keys.Remove(keyId);
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
                Status = interactive.Status
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
