using Adventure.Dungeon;
using Adventure.Shared.Network.Messages;
using UnityEditor;
using UnityEngine;

namespace Adventure.EditorTools
{
    /// <summary>
    /// Lightweight editor window to exercise the dungeon generator and review the produced layout summary.
    /// </summary>
    public class DungeonLayoutGeneratorWindow : EditorWindow
    {
        private DungeonGenerationManager? generator;
        private DungeonLayoutSummary? lastLayout;

        [MenuItem("Adventure/Dungeons/Generate Layout Preview")]
        public static void ShowWindow()
        {
            GetWindow<DungeonLayoutGeneratorWindow>("Dungeon Layout");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Dungeon Layout Generator", EditorStyles.boldLabel);
            generator = (DungeonGenerationManager)EditorGUILayout.ObjectField("Generator", generator, typeof(DungeonGenerationManager), true);

            if (generator == null)
            {
                EditorGUILayout.HelpBox("Assign a DungeonGenerationManager from the scene to preview.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Generate"))
            {
                lastLayout = generator.GenerateLayout();
                Debug.Log($"Generated layout with {lastLayout.Rooms.Count} rooms and {lastLayout.Doors.Count} doors.");
            }

            if (lastLayout != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Rooms", EditorStyles.boldLabel);
                foreach (var room in lastLayout.Rooms)
                {
                    EditorGUILayout.LabelField($"{room.SequenceIndex}: {room.TemplateId} ({room.RoomType})");
                }

                EditorGUILayout.LabelField("Doors", EditorStyles.boldLabel);
                foreach (var door in lastLayout.Doors)
                {
                    EditorGUILayout.LabelField($"{door.DoorId} [{door.State}] -> {door.ToRoomId}");
                }
            }
        }
    }
}
