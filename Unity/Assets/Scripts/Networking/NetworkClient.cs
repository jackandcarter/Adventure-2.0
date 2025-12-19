using System;
using System.Text.Json;
using System.Threading.Tasks;
using Adventure.Shared.Network.Messages;
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

        [SerializeField]
        private WebSocketTransport webSocketTransport;

        private bool connected;

        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<string> MessageReceived;

        private void Awake()
        {
            if (webSocketTransport == null)
            {
                webSocketTransport = GetComponent<WebSocketTransport>();
                if (webSocketTransport == null)
                {
                    webSocketTransport = gameObject.AddComponent<WebSocketTransport>();
                }
            }

            webSocketTransport.Connected += OnTransportConnected;
            webSocketTransport.Disconnected += OnTransportDisconnected;
            webSocketTransport.MessageReceived += OnTransportMessageReceived;
        }

        private void OnDestroy()
        {
            if (webSocketTransport == null)
            {
                return;
            }

            webSocketTransport.Connected -= OnTransportConnected;
            webSocketTransport.Disconnected -= OnTransportDisconnected;
            webSocketTransport.MessageReceived -= OnTransportMessageReceived;
        }

        public void Configure(string hostName, int portNumber)
        {
            host = hostName;
            port = portNumber;
            webSocketTransport?.Configure(hostName, portNumber);
        }

        public async Task ConnectAsync()
        {
            if (webSocketTransport == null)
            {
                await Task.Delay(50);
                connected = true;
                Connected?.Invoke();
                Debug.Log($"NetworkClient connected to {host}:{port}");
                return;
            }

            webSocketTransport.Configure(host, port);
            await webSocketTransport.ConnectAsync();
        }

        public void Disconnect(string reason = "client requested")
        {
            if (!connected)
            {
                return;
            }

            if (webSocketTransport != null)
            {
                webSocketTransport.Disconnect(reason);
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

            var envelope = new MessageEnvelope<TPayload>
            {
                Type = route,
                Payload = payload
            };

            var serialized = JsonSerializer.Serialize(envelope, serializerOptions);
            Debug.Log($"Sending {route} -> {serialized}");
            webSocketTransport?.EnqueueSend(serialized);
        }

        public void SendEnvelope<TPayload>(MessageEnvelope<TPayload> envelope) where TPayload : class
        {
            if (!connected)
            {
                Debug.LogWarning($"Unable to send envelope {envelope.Type}; not connected.");
                return;
            }

            var serialized = JsonSerializer.Serialize(envelope, serializerOptions);
            Debug.Log($"Sending envelope {envelope.Type}: {serialized}");
            webSocketTransport?.EnqueueSend(serialized);
        }

        public bool IsConnected => connected;

        /// <summary>
        /// Allows tests and offline flows to push incoming messages into the transport.
        /// </summary>
        public void SimulateIncoming(string rawJson)
        {
            MessageReceived?.Invoke(rawJson);
        }

        private void OnTransportConnected()
        {
            connected = true;
            Connected?.Invoke();
            Debug.Log($"NetworkClient connected to {host}:{port}");
        }

        private void OnTransportDisconnected(string reason)
        {
            connected = false;
            Disconnected?.Invoke(reason);
            Debug.LogWarning($"NetworkClient disconnected: {reason}");
        }

        private void OnTransportMessageReceived(string rawJson)
        {
            MessageReceived?.Invoke(rawJson);
        }
    }
}
