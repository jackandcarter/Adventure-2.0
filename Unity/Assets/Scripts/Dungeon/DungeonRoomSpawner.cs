using System.Collections.Generic;
using Adventure.ScriptableObjects;
using Adventure.Shared.Network.Messages;
using UnityEngine;

namespace Adventure.Dungeon
{
    /// <summary>
    /// Instantiates authored room prefabs into world space based on a generated layout summary.
    /// </summary>
    public class DungeonRoomSpawner : MonoBehaviour
    {
        [SerializeField]
        private DungeonThemeDefinition? theme;

        [SerializeField]
        private Transform? roomParent;

        [SerializeField]
        private float cellSize = 20f;

        private readonly Dictionary<string, RoomTemplateDefinition> templateLookup = new();
        private readonly Dictionary<string, GameObject> spawnedRooms = new();

        private void Awake()
        {
            if (roomParent == null)
            {
                roomParent = transform;
            }

            BuildTemplateLookup();
        }

        public void BuildTemplateLookup()
        {
            templateLookup.Clear();
            if (theme == null)
            {
                return;
            }

            foreach (var template in theme.RoomTemplates)
            {
                if (template != null && !templateLookup.ContainsKey(template.Id))
                {
                    templateLookup.Add(template.Id, template);
                }
            }
        }

        public void ClearSpawned()
        {
            foreach (var instance in spawnedRooms.Values)
            {
                if (instance != null)
                {
                    DestroyImmediate(instance);
                }
            }

            spawnedRooms.Clear();
        }

        public void SpawnLayout(DungeonLayoutSummary layout)
        {
            if (layout == null)
            {
                return;
            }

            ClearSpawned();
            BuildTemplateLookup();

            foreach (var room in layout.Rooms)
            {
                if (!templateLookup.TryGetValue(room.TemplateId, out var template) || template.RoomPrefab == null)
                {
                    Debug.LogWarning($"Missing prefab for template {room.TemplateId}; skipping spawn.");
                    continue;
                }

                Vector3 position = new(room.GridX * cellSize, 0f, room.GridY * cellSize);
                var instance = Instantiate(template.RoomPrefab, position, Quaternion.identity, roomParent);
                instance.name = $"Room_{room.SequenceIndex}_{template.DisplayName}";
                spawnedRooms.Add(room.RoomId, instance);
            }
        }
    }
}
