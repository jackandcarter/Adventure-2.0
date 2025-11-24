using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Adventure.EditorTools
{
    public static class RoomPrefabValidator
    {
        private static readonly string[] RequiredTags = { "Socket", "Door", "Trigger" };

        public static void Validate(GameObject prefab, List<string> errors)
        {
            if (prefab == null)
            {
                errors.Add("Room prefab is missing.");
                return;
            }

            if (!PrefabUtility.IsPartOfAnyPrefab(prefab))
            {
                errors.Add($"{prefab.name}: Assigned object is not a prefab instance.");
            }

            foreach (var tag in RequiredTags)
            {
                if (!HasChildWithTag(prefab, tag))
                {
                    errors.Add($"{prefab.name}: Missing child tagged '{tag}'.");
                }
            }

            ValidateDoorSockets(prefab, errors);
            ValidateTriggerVolumes(prefab, errors);
        }

        private static bool HasChildWithTag(GameObject prefab, string tag)
        {
            return prefab.GetComponentsInChildren<Transform>(true).Any(t => t.CompareTag(tag));
        }

        private static void ValidateDoorSockets(GameObject prefab, List<string> errors)
        {
            var sockets = prefab.GetComponentsInChildren<Transform>(true).Where(t => t.CompareTag("Socket")).ToList();
            var doors = prefab.GetComponentsInChildren<Transform>(true).Where(t => t.CompareTag("Door")).ToList();

            if (doors.Count > sockets.Count)
            {
                errors.Add($"{prefab.name}: Found more doors ({doors.Count}) than sockets ({sockets.Count}). Check socket tagging.");
            }
        }

        private static void ValidateTriggerVolumes(GameObject prefab, List<string> errors)
        {
            var triggers = prefab.GetComponentsInChildren<Transform>(true).Where(t => t.CompareTag("Trigger")).ToList();
            foreach (var trigger in triggers)
            {
                if (trigger.GetComponent<Collider>() == null)
                {
                    errors.Add($"{prefab.name}/{trigger.name}: Trigger is tagged but missing a Collider component.");
                }
                else if (!trigger.GetComponent<Collider>().isTrigger)
                {
                    errors.Add($"{prefab.name}/{trigger.name}: Collider must be marked as Trigger.");
                }
            }
        }
    }
}
