using System;
using System.Collections.Generic;
using Adventure.ScriptableObjects;
using Adventure.Shared.Network.Messages;
using UnityEngine;

namespace Adventure.Dungeon
{
    /// <summary>
    /// Builds a simple grid-based dungeon layout using room template definitions and surfaces
    /// a summary that can be consumed by the server or by local spawning systems.
    /// </summary>
    public class DungeonGenerationManager : MonoBehaviour
    {
        [SerializeField]
        private DungeonThemeDefinition? theme;

        [SerializeField]
        private int gridWidth = 5;

        [SerializeField]
        private int gridHeight = 5;

        [SerializeField]
        [Tooltip("How long the guaranteed path should be (including start and boss).")]
        private int mainPathLength = 6;

        [SerializeField]
        [Tooltip("Chance [0,1] that a branch spawns off the main path when space allows.")]
        [Range(0f, 1f)]
        private float branchChance = 0.35f;

        [SerializeField]
        [Tooltip("If true, generation will attempt to add a rare secret staircase branch.")]
        private bool allowSecretStaircase = true;

        public DungeonLayoutSummary GenerateLayout(System.Random? random = null)
        {
            if (theme == null)
            {
                throw new InvalidOperationException("DungeonThemeDefinition is required.");
            }

            random ??= new System.Random();
            var occupied = new Dictionary<Vector2Int, PlacedRoom>();
            var layout = new DungeonLayoutSummary
            {
                DungeonId = Guid.NewGuid().ToString()
            };

            Vector2Int cursor = new(gridWidth / 2, gridHeight / 2);
            PlaceRoom(cursor, ChooseTemplate(RoomTemplateType.Safe, random), layout, occupied, false, random);

            // Build the main path ending in a boss room
            for (int i = 1; i < mainPathLength; i++)
            {
                var nextDirection = PickDirection(cursor, random, occupied);
                if (nextDirection == Vector2Int.zero)
                {
                    break;
                }
                cursor += nextDirection;
                bool isFinalStep = i == mainPathLength - 1;
                var targetType = isFinalStep ? RoomTemplateType.Boss : RoomTemplateType.Enemy;
                var template = ChooseTemplate(targetType, random);
                PlaceRoom(cursor, template, layout, occupied, ShouldLock(template), random, isFinalStep);

                if (!isFinalStep && random.NextDouble() < branchChance)
                {
                    TrySpawnBranch(cursor, layout, occupied, random);
                }
            }

            if (allowSecretStaircase)
            {
                TryPlaceSecretStaircase(layout, occupied, random);
            }

            BuildDoorGraph(layout, occupied);
            return layout;
        }

        private void TrySpawnBranch(Vector2Int origin, DungeonLayoutSummary layout, Dictionary<Vector2Int, PlacedRoom> occupied, System.Random random)
        {
            var direction = PickDirection(origin, random, occupied);
            if (direction == Vector2Int.zero)
            {
                return;
            }
            var target = origin + direction;
            if (occupied.ContainsKey(target))
            {
                return;
            }

            var branchType = random.NextDouble() > 0.5 ? RoomTemplateType.Treasure : RoomTemplateType.Illusion;
            var template = ChooseTemplate(branchType, random);
            PlaceRoom(target, template, layout, occupied, ShouldLock(template), random);
        }

        private void TryPlaceSecretStaircase(DungeonLayoutSummary layout, Dictionary<Vector2Int, PlacedRoom> occupied, System.Random random)
        {
            foreach (var kvp in occupied)
            {
                var room = kvp.Value;
                if (!room.Template.CanSpawnSecretStaircase)
                {
                    continue;
                }

                var direction = PickDirection(kvp.Key, random, occupied, allowVisited: false);
                if (direction == Vector2Int.zero)
                {
                    continue;
                }
                var candidate = kvp.Key + direction;
                if (occupied.ContainsKey(candidate))
                {
                    continue;
                }

                var staircaseTemplate = ChooseTemplate(RoomTemplateType.StaircaseDown, random);
                PlaceRoom(candidate, staircaseTemplate, layout, occupied, true, random, false, true);
                return;
            }
        }

        private void BuildDoorGraph(DungeonLayoutSummary layout, Dictionary<Vector2Int, PlacedRoom> occupied)
        {
            var offsets = new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            var added = new HashSet<string>();
            foreach (var kvp in occupied)
            {
                foreach (var offset in offsets)
                {
                    var neighborPos = kvp.Key + offset;
                    if (!occupied.TryGetValue(neighborPos, out var neighbor))
                    {
                        continue;
                    }

                    string doorId = $"{kvp.Value.RoomId}->{neighbor.RoomId}";
                    if (added.Contains(doorId))
                    {
                        continue;
                    }

                    bool locked = neighbor.IsLocked || kvp.Value.IsLocked;
                    layout.Doors.Add(new DungeonDoorState
                    {
                        DoorId = doorId,
                        FromRoomId = kvp.Value.RoomId,
                        ToRoomId = neighbor.RoomId,
                        RequiredKeyId = locked ? "dungeon-key" : null,
                        State = locked ? DoorState.Locked : DoorState.Closed
                    });
                    added.Add(doorId);
                }
            }
        }

        private void PlaceRoom(Vector2Int gridPos, RoomTemplateDefinition template, DungeonLayoutSummary layout, Dictionary<Vector2Int, PlacedRoom> occupied, bool locked, System.Random random, bool isBoss = false, bool isBasement = false)
        {
            var roomId = Guid.NewGuid().ToString();
            occupied[gridPos] = new PlacedRoom
            {
                RoomId = roomId,
                Template = template,
                GridPosition = gridPos,
                IsLocked = locked,
                IsBasement = isBasement
            };

            layout.Rooms.Add(new DungeonRoomSummary
            {
                RoomId = roomId,
                TemplateId = template.Id,
                RoomType = template.RoomType,
                SequenceIndex = layout.Rooms.Count,
                GridX = gridPos.x,
                GridY = gridPos.y
            });

            // Include environment defaults so server can validate
            if (template.ToExportModel() is RoomTemplatePayload payload)
            {
                foreach (var env in payload.EnvironmentStates)
                {
                    layout.EnvironmentStates.Add(new EnvironmentStateSnapshot
                    {
                        RoomId = roomId,
                        StateId = env.StateId,
                        Value = env.DefaultValue
                    });
                }
            }

            if (locked)
            {
                layout.Interactives.Add(new InteractiveObjectState
                {
                    ObjectId = $"lock-{roomId}",
                    RoomId = roomId,
                    Kind = "lock",
                    GrantedKeyId = null,
                    Status = InteractiveStatus.Available
                });
            }

            if (template.ProvidesKeys.Count > 0)
            {
                var grantedKey = template.ProvidesKeys[random.Next(template.ProvidesKeys.Count)];
                layout.Interactives.Add(new InteractiveObjectState
                {
                    ObjectId = $"key-{roomId}",
                    RoomId = roomId,
                    Kind = "chest",
                    GrantedKeyId = grantedKey,
                    Status = InteractiveStatus.Available
                });
            }
        }

        private bool ShouldLock(RoomTemplateDefinition template)
        {
            if (template.NeverLocked)
            {
                return false;
            }

            return template.AllowsLockedVariant && (template.RoomType == RoomTemplateType.Treasure || template.RoomType == RoomTemplateType.Miniboss || template.RoomType == RoomTemplateType.StaircaseDown);
        }

        private RoomTemplateDefinition ChooseTemplate(RoomTemplateType type, System.Random random)
        {
            if (theme == null)
            {
                throw new InvalidOperationException("DungeonThemeDefinition is required.");
            }

            var candidates = theme.RoomTemplates.FindAll(t => t.RoomType == type);
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException($"Theme {theme.DisplayName} is missing a template for {type}.");
            }

            return candidates[random.Next(candidates.Count)];
        }

        private Vector2Int PickDirection(Vector2Int origin, System.Random random, Dictionary<Vector2Int, PlacedRoom> occupied, bool allowVisited = false)
        {
            var options = new List<Vector2Int>();
            var deltas = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            foreach (var delta in deltas)
            {
                var candidate = origin + delta;
                if (candidate.x < 0 || candidate.x >= gridWidth || candidate.y < 0 || candidate.y >= gridHeight)
                {
                    continue;
                }

                if (!allowVisited && occupied.ContainsKey(candidate))
                {
                    continue;
                }

                options.Add(delta);
            }

            if (options.Count == 0)
            {
                return Vector2Int.zero;
            }

            return options[random.Next(options.Count)];
        }

        private class PlacedRoom
        {
            public string RoomId = string.Empty;
            public RoomTemplateDefinition Template = null!;
            public Vector2Int GridPosition;
            public bool IsLocked;
            public bool IsBasement;
        }
    }
}
