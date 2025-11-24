using System;
using System.Collections.Generic;
using System.Linq;
using Adventure.Shared.Network.Messages;

namespace Adventure.Server.Generation
{
    public class DungeonGenerator
    {
        private readonly Random random;

        public DungeonGenerator(int? seed = null)
        {
            random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        }

        public GeneratedDungeon Generate(DungeonTemplate template, DungeonGenerationSettings settings)
        {
            if (template.Rooms.Count == 0)
            {
                throw new InvalidOperationException("Dungeon template contains no rooms.");
            }

            var startRoomTemplate = ChooseRoom(template.Rooms, RoomTemplateType.Start)
                ?? throw new InvalidOperationException("Dungeon template requires at least one start room.");
            var bossRoomTemplate = ChooseRoom(template.Rooms, RoomTemplateType.Boss)
                ?? throw new InvalidOperationException("Dungeon template requires at least one boss room.");

            var combatRooms = ChooseRooms(template.Rooms, RoomTemplateType.Combat, settings.CombatRooms);
            var puzzleRooms = ChooseRooms(template.Rooms, RoomTemplateType.Puzzle, settings.PuzzleRooms);

            var orderedTemplates = new List<RoomTemplate> { startRoomTemplate };
            orderedTemplates.AddRange(combatRooms);
            orderedTemplates.AddRange(puzzleRooms);
            orderedTemplates.Add(bossRoomTemplate);

            var generatedRooms = new List<GeneratedRoom>();
            var generatedDoors = new List<GeneratedDoor>();
            var generatedInteractives = new List<GeneratedInteractive>();
            var generatedEnvironmentStates = new List<GeneratedEnvironmentState>();
            var placedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < orderedTemplates.Count; i++)
            {
                var templateRoom = orderedTemplates[i];
                var roomId = $"{templateRoom.TemplateId}-{i + 1}";
                var generatedRoom = new GeneratedRoom(roomId, templateRoom, i);
                generatedRooms.Add(generatedRoom);

                foreach (var env in templateRoom.EnvironmentStates)
                {
                    var envState = new GeneratedEnvironmentState
                    {
                        RoomId = roomId,
                        StateId = env.StateId,
                        Value = env.DefaultValue
                    };
                    generatedRoom.EnvironmentStates.Add(envState);
                    generatedEnvironmentStates.Add(envState);
                }

                foreach (var interactive in templateRoom.InteractiveObjects)
                {
                    var instance = new GeneratedInteractive
                    {
                        ObjectId = string.IsNullOrWhiteSpace(interactive.ObjectId)
                            ? $"{roomId}-interactive-{generatedRoom.InteractiveObjects.Count + 1}"
                            : interactive.ObjectId,
                        RoomId = roomId,
                        Kind = interactive.Kind,
                        GrantsKeyId = interactive.GrantsKeyId,
                        RequiresKeyId = interactive.RequiresKeyId,
                        ActivatesTriggerId = interactive.ActivatesTriggerId
                    };

                    generatedRoom.InteractiveObjects.Add(instance);
                    generatedInteractives.Add(instance);

                    if (!string.IsNullOrWhiteSpace(instance.GrantsKeyId))
                    {
                        placedKeys.Add(instance.GrantsKeyId);
                    }
                }

                foreach (var keyId in templateRoom.ProvidesKeys)
                {
                    placedKeys.Add(keyId);
                }
            }

            for (var i = 0; i < generatedRooms.Count - 1; i++)
            {
                var fromRoom = generatedRooms[i];
                var toRoom = generatedRooms[i + 1];
                var templateDoor = fromRoom.Template.Doors.FirstOrDefault() ?? new DoorTemplate
                {
                    DoorId = $"{fromRoom.RoomId}-to-{toRoom.RoomId}",
                    SocketId = "exit",
                    StartsLocked = false
                };

                if (!string.IsNullOrWhiteSpace(templateDoor.RequiredKeyId) && !placedKeys.Contains(templateDoor.RequiredKeyId))
                {
                    PlaceKeyInPreviousRoom(generatedRooms, generatedInteractives, placedKeys, i, templateDoor.RequiredKeyId);
                }

                var door = new GeneratedDoor
                {
                    DoorId = templateDoor.DoorId,
                    FromRoomId = fromRoom.RoomId,
                    ToRoomId = toRoom.RoomId,
                    RequiredKeyId = templateDoor.RequiredKeyId,
                    State = templateDoor.StartsLocked || !string.IsNullOrWhiteSpace(templateDoor.RequiredKeyId)
                        ? DoorState.Locked
                        : DoorState.Closed
                };

                fromRoom.Doors.Add(door);
                generatedDoors.Add(door);
            }

            return new GeneratedDungeon(template.DungeonId, generatedRooms, generatedDoors, generatedInteractives, generatedEnvironmentStates);
        }

        private RoomTemplate? ChooseRoom(IEnumerable<RoomTemplate> rooms, RoomTemplateType type)
        {
            var matching = rooms.Where(r => r.RoomType == type).ToList();
            return matching.Count == 0 ? null : matching[random.Next(matching.Count)];
        }

        private IEnumerable<RoomTemplate> ChooseRooms(IEnumerable<RoomTemplate> rooms, RoomTemplateType type, int count)
        {
            var matching = rooms.Where(r => r.RoomType == type).ToList();
            if (matching.Count == 0 || count <= 0)
            {
                return Enumerable.Empty<RoomTemplate>();
            }

            return Enumerable.Range(0, count)
                .Select(_ => matching[random.Next(matching.Count)])
                .ToList();
        }

        private void PlaceKeyInPreviousRoom(
            IReadOnlyList<GeneratedRoom> generatedRooms,
            List<GeneratedInteractive> allInteractives,
            HashSet<string> placedKeys,
            int roomIndex,
            string requiredKeyId)
        {
            for (var candidateIndex = roomIndex; candidateIndex >= 0; candidateIndex--)
            {
                var candidateRoom = generatedRooms[candidateIndex];
                var keyGrantingInteractive = candidateRoom.InteractiveObjects.FirstOrDefault(i => string.IsNullOrEmpty(i.GrantsKeyId));
                if (keyGrantingInteractive != null)
                {
                    keyGrantingInteractive.GrantsKeyId = requiredKeyId;
                    placedKeys.Add(requiredKeyId);
                    return;
                }
            }

            var fallbackRoom = generatedRooms[Math.Max(0, roomIndex)];
            var generatedInteractive = new GeneratedInteractive
            {
                ObjectId = $"{fallbackRoom.RoomId}-autokey",
                RoomId = fallbackRoom.RoomId,
                Kind = "auto_key",
                GrantsKeyId = requiredKeyId
            };

            fallbackRoom.InteractiveObjects.Add(generatedInteractive);
            allInteractives.Add(generatedInteractive);
            placedKeys.Add(requiredKeyId);
        }
    }
}
