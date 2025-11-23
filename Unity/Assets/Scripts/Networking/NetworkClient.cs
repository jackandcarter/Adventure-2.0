using System;
using System.Collections.Generic;
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

        private bool connected;

        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly Queue<string> simulatedIncoming = new();

        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<string> MessageReceived;

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

            var envelope = new MessageEnvelope<TPayload>
            {
                Type = route,
                Payload = payload
            };

            var serialized = JsonSerializer.Serialize(envelope, serializerOptions);
            Debug.Log($"Sending {route} -> {serialized}");
            // Implementation placeholder for transport-level send.
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
        }

        public bool IsConnected => connected;

        /// <summary>
        /// Allows tests and offline flows to push incoming messages into the transport.
        /// </summary>
        public void SimulateIncoming(string rawJson)
        {
            simulatedIncoming.Enqueue(rawJson);
        }

        private void Update()
        {
            while (simulatedIncoming.Count > 0)
            {
                var next = simulatedIncoming.Dequeue();
                MessageReceived?.Invoke(next);
            }
        }
    }
}
