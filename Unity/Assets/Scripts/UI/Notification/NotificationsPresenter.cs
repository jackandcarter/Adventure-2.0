using System.Collections.Generic;
using Adventure.Networking;
using Adventure.Shared.Network.Messages;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure.UI.Notification
{
    /// <summary>
    /// Simple notification stacker that instantiates a prefab for each message.
    /// </summary>
    public class NotificationsPresenter : MonoBehaviour
    {
        [SerializeField]
        private GameObject notificationPrefab;

        [SerializeField]
        private Transform notificationRoot;

        [SerializeField]
        private int maxVisible = 4;

        [SerializeField]
        private float lifetimeSeconds = 4f;

        private readonly Queue<GameObject> active = new();

        private void Awake()
        {
            if (notificationRoot == null)
            {
                notificationRoot = transform;
            }
        }

        public void Bind(GameStateClient gameStateClient)
        {
            if (gameStateClient == null)
            {
                return;
            }

            gameStateClient.ErrorReceived += OnError;
        }

        public void Show(string message)
        {
            if (notificationPrefab == null || notificationRoot == null)
            {
                Debug.LogWarning($"NotificationPrefab missing: {message}");
                return;
            }

            var instance = Instantiate(notificationPrefab, notificationRoot);
            var text = instance.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = message;
            }

            active.Enqueue(instance);
            Trim();
            Destroy(instance, lifetimeSeconds);
        }

        private void OnError(ErrorResponse error)
        {
            Show($"{error.Code.ToUpperInvariant()}: {error.Message}");
        }

        private void Trim()
        {
            while (active.Count > maxVisible)
            {
                var toRemove = active.Dequeue();
                if (toRemove != null)
                {
                    Destroy(toRemove);
                }
            }
        }
    }
}
