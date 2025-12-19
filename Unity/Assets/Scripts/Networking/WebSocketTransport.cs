using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Adventure.Networking
{
    /// <summary>
    /// WebSocket transport that queues outbound messages, delivers inbound messages on the main thread,
    /// and supports reconnection by re-invoking ConnectAsync.
    /// </summary>
    public class WebSocketTransport : MonoBehaviour
    {
        [SerializeField]
        private string host = "127.0.0.1";

        [SerializeField]
        private int port = 7777;

        [SerializeField]
        private string path = "/ws";

        [SerializeField]
        private int sendBudgetPerFrame = 8;

        [SerializeField]
        private int maxQueuedMessages = 256;

        private readonly ConcurrentQueue<string> outbound = new();
        private readonly ConcurrentQueue<string> inbound = new();
        private readonly SemaphoreSlim sendLock = new(1, 1);

        private ClientWebSocket socket;
        private CancellationTokenSource receiveCts;
        private bool connecting;

        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<string> MessageReceived;

        public bool IsConnected => socket != null && socket.State == WebSocketState.Open;

        public void Configure(string hostName, int portNumber)
        {
            host = hostName;
            port = portNumber;
        }

        public async Task ConnectAsync()
        {
            if (connecting || IsConnected)
            {
                return;
            }

            connecting = true;
            receiveCts?.Cancel();
            receiveCts = new CancellationTokenSource();
            socket?.Dispose();
            socket = new ClientWebSocket();

            try
            {
                var uri = new Uri($"ws://{host}:{port}{path}");
                await socket.ConnectAsync(uri, receiveCts.Token);
                Connected?.Invoke();
                _ = ReceiveLoopAsync(socket, receiveCts.Token);
            }
            catch (Exception ex)
            {
                socket?.Dispose();
                socket = null;
                Disconnected?.Invoke(ex.Message);
            }
            finally
            {
                connecting = false;
            }
        }

        public void Disconnect(string reason = "client requested")
        {
            receiveCts?.Cancel();
            if (socket == null)
            {
                return;
            }

            _ = socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
            socket.Dispose();
            socket = null;
            Disconnected?.Invoke(reason);
        }

        public void EnqueueSend(string payload)
        {
            if (outbound.Count >= maxQueuedMessages)
            {
                Debug.LogWarning("WebSocketTransport outbound queue full. Dropping message.");
                return;
            }

            outbound.Enqueue(payload);
        }

        private async Task ReceiveLoopAsync(ClientWebSocket client, CancellationToken token)
        {
            var buffer = new byte[8192];
            try
            {
                while (client.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var builder = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Disconnected?.Invoke("server closed");
                            return;
                        }

                        builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    inbound.Enqueue(builder.ToString());
                }
            }
            catch (Exception ex)
            {
                Disconnected?.Invoke(ex.Message);
            }
        }

        private void Update()
        {
            FlushOutbound();
            FlushInbound();
        }

        private void FlushOutbound()
        {
            if (!IsConnected)
            {
                return;
            }

            for (var i = 0; i < sendBudgetPerFrame && outbound.TryDequeue(out var payload); i++)
            {
                _ = SendAsync(payload);
            }
        }

        private async Task SendAsync(string payload)
        {
            if (!IsConnected)
            {
                return;
            }

            var buffer = Encoding.UTF8.GetBytes(payload);
            await sendLock.WaitAsync();
            try
            {
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Disconnected?.Invoke(ex.Message);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private void FlushInbound()
        {
            while (inbound.TryDequeue(out var payload))
            {
                MessageReceived?.Invoke(payload);
            }
        }
    }
}
