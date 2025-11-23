using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Adventure.Networking
{
    /// <summary>
    /// Lightweight network abstraction that relays input and high-level RPCs to the server.
    /// This client is intentionally minimal so it can be driven directly from scene controllers.
    /// </summary>
    public class NetworkClient : MonoBehaviour
    {
        [SerializeField]
        private string host = "127.0.0.1";

        [SerializeField]
        private int port = 7777;

        private bool connected;

        public event Action Connected;
        public event Action<string> Disconnected;

        public void Configure(string hostName, int portNumber)
        {
            host = hostName;
            port = portNumber;
        }

        public async Task ConnectAsync()
        {
            // In a full implementation this would open a socket/websocket. For now we simulate an async connect
            // so bootstrapping code can await network readiness.
            await Task.Delay(50);
            connected = true;
            Connected?.Invoke();
            Debug.Log($"NetworkClient connected to {host}:{port}");
        }

        public void Disconnect(string reason = "client requested")
        {
            if (!connected)
            {
                return;
            }

            connected = false;
            Disconnected?.Invoke(reason);
            Debug.LogWarning($"NetworkClient disconnected: {reason}");
        }

        public void SendReliable<TPayload>(string route, TPayload payload)
        {
            if (!connected)
            {
                Debug.LogWarning($"Tried to send on closed connection: {route}");
                return;
            }

            Debug.Log($"Sending {route} -> {payload}");
            // Implementation placeholder for transport-level send.
        }
    }
}
