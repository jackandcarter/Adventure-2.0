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

            var archetypePlan = BuildArchetypePlan(settings);
            var orderedTemplates = ChooseTemplatesForArchetypes(template.Rooms, archetypePlan);

            var generatedRooms = new List<GeneratedRoom>();
            var generatedDoors = new List<GeneratedDoor>();
            var generatedInteractives = new List<GeneratedInteractive>();
            var generatedEnvironmentStates = new List<GeneratedEnvironmentState>();
            var placedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < orderedTemplates.Count; i++)
            {
                var (templateRoom, archetype) = orderedTemplates[i];
                var roomId = $"{templateRoom.TemplateId}-{i + 1}";
                var features = ApplyArchetypeFeatures(templateRoom.Features, archetype);
                var generatedRoom = new GeneratedRoom(roomId, templateRoom, i, archetype, features);
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
                        GrantsKeyTag = interactive.GrantsKeyTag,
                        RequiresKeyId = interactive.RequiresKeyId,
                        RequiresKeyTag = interactive.RequiresKeyTag,
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
                    StartsLocked = false,
                    ConfigId = "default"
                };

                var requiredKeyId = templateDoor.RequiredKeyId;
                if (templateDoor.RequiredKeyTag.HasValue && string.IsNullOrWhiteSpace(requiredKeyId))
                {
                    requiredKeyId = $"{templateDoor.RequiredKeyTag.Value.ToString().ToLowerInvariant()}-{i + 1}";
                }

                if (!string.IsNullOrWhiteSpace(requiredKeyId) && !placedKeys.Contains(requiredKeyId))
                {
                    PlaceKeyInPreviousRoom(
                        generatedRooms,
                        generatedInteractives,
                        placedKeys,
                        i,
                        requiredKeyId!,
                        templateDoor.RequiredKeyTag);
                }

                var door = new GeneratedDoor
                {
                    DoorId = templateDoor.DoorId,
                    FromRoomId = fromRoom.RoomId,
                    ToRoomId = toRoom.RoomId,
                    RequiredKeyId = requiredKeyId,
                    RequiredKeyTag = templateDoor.RequiredKeyTag,
                    State = templateDoor.StartsLocked || !string.IsNullOrWhiteSpace(requiredKeyId)
                        ? DoorState.Locked
                        : DoorState.Open,
                    ConfigId = templateDoor.ConfigId
                };

                fromRoom.Doors.Add(door);
                generatedDoors.Add(door);
            }

            return new GeneratedDungeon(
                template.DungeonId,
                generatedRooms,
                generatedDoors,
                generatedInteractives,
                generatedEnvironmentStates,
                template.DoorConfigs);
        }

        private List<(RoomTemplate Template, RoomArchetype Archetype)> ChooseTemplatesForArchetypes(
            IReadOnlyList<RoomTemplate> templates,
            IReadOnlyList<RoomArchetype> archetypes)
        {
            var list = new List<(RoomTemplate, RoomArchetype)>();
            foreach (var archetype in archetypes)
            {
                var template = templates[random.Next(templates.Count)];
                list.Add((template, archetype));
            }

            return list;
        }

        private List<RoomArchetype> BuildArchetypePlan(DungeonGenerationSettings settings)
        {
            var archetypes = new List<RoomArchetype> { RoomArchetype.Safe };

            var enemySlots = Math.Max(0, settings.EnemyRooms);
            var treasureSlots = Math.Max(0, settings.TreasureRooms);
            for (var i = 0; i < enemySlots; i++)
            {
                archetypes.Add(RoomArchetype.Enemy);
            }

            for (var i = 0; i < treasureSlots; i++)
            {
                archetypes.Insert(Math.Max(1, archetypes.Count - 2), RoomArchetype.Treasure);
            }

            if (settings.IncludeMiniboss)
            {
                archetypes.Add(RoomArchetype.MiniBoss);
            }

            archetypes.Add(RoomArchetype.Boss);

            SprinkleHazards(archetypes);
            return archetypes;
        }

        private void SprinkleHazards(List<RoomArchetype> archetypes)
        {
            var enemyIndexes = archetypes
                .Select((value, index) => (value, index))
                .Where(tuple => tuple.value == RoomArchetype.Enemy)
                .Select(tuple => tuple.index)
                .ToList();

            if (enemyIndexes.Count == 0)
            {
                return;
            }

            var trapIndex = enemyIndexes[random.Next(enemyIndexes.Count)];
            archetypes[trapIndex] = RoomArchetype.Trap;

            if (enemyIndexes.Count > 1)
            {
                var illusionIndex = enemyIndexes[random.Next(enemyIndexes.Count)];
                archetypes[illusionIndex] = RoomArchetype.Illusion;
            }
        }

        private void PlaceKeyInPreviousRoom(
            IReadOnlyList<GeneratedRoom> generatedRooms,
            List<GeneratedInteractive> allInteractives,
            HashSet<string> placedKeys,
            int roomIndex,
            string requiredKeyId,
            KeyTag? keyTag)
        {
            for (var candidateIndex = roomIndex; candidateIndex >= 0; candidateIndex--)
            {
                var candidateRoom = generatedRooms[candidateIndex];
                var keyGrantingInteractive = candidateRoom.InteractiveObjects.FirstOrDefault(i => string.IsNullOrEmpty(i.GrantsKeyId));
                if (keyGrantingInteractive != null)
                {
                    keyGrantingInteractive.GrantsKeyId = requiredKeyId;
                    keyGrantingInteractive.GrantsKeyTag = keyTag;
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
                GrantsKeyId = requiredKeyId,
                GrantsKeyTag = keyTag
            };

            fallbackRoom.InteractiveObjects.Add(generatedInteractive);
            allInteractives.Add(generatedInteractive);
            placedKeys.Add(requiredKeyId);
        }

        private static RoomFeature ApplyArchetypeFeatures(RoomFeature baseFeatures, RoomArchetype archetype)
        {
            return archetype switch
            {
                RoomArchetype.Treasure => baseFeatures | RoomFeature.TreasureChest,
                RoomArchetype.Illusion => baseFeatures | RoomFeature.Illusion,
                RoomArchetype.Trap => baseFeatures | RoomFeature.Trap,
                _ => baseFeatures
            };
        }
    }
}
