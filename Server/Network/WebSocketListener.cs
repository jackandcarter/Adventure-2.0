using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Adventure.Server.Core.Sessions;
using Adventure.Shared.Network.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Adventure.Server.Network
{
    public class WebSocketListenerOptions
    {
        public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(20);
        public int ReceiveBufferSize { get; set; } = 8 * 1024;
    }

    public class WebSocketConnectionRegistry
    {
        private readonly ConcurrentDictionary<string, string> connectionBySession = new();
        private readonly ConcurrentDictionary<string, ConnectionState> connectionState = new();

        public void Register(string connectionId)
        {
            connectionState[connectionId] = new ConnectionState(connectionId);
        }

        public void BindSession(string connectionId, string sessionId)
        {
            connectionBySession[sessionId] = connectionId;
            if (connectionState.TryGetValue(connectionId, out var state))
            {
                state.SessionId = sessionId;
            }
        }

        public void Touch(string connectionId)
        {
            if (connectionState.TryGetValue(connectionId, out var state))
            {
                state.LastSeenUtc = DateTimeOffset.UtcNow;
            }
        }

        public string? Remove(string connectionId)
        {
            if (connectionState.TryRemove(connectionId, out var state) && !string.IsNullOrEmpty(state.SessionId))
            {
                connectionBySession.TryRemove(state.SessionId, out _);
                return state.SessionId;
            }

            return null;
        }

        public bool TryGetConnectionId(string sessionId, out string connectionId)
        {
            return connectionBySession.TryGetValue(sessionId, out connectionId!);
        }

        private class ConnectionState
        {
            public ConnectionState(string connectionId)
            {
                ConnectionId = connectionId;
                LastSeenUtc = DateTimeOffset.UtcNow;
            }

            public string ConnectionId { get; }
            public string? SessionId { get; set; }
            public DateTimeOffset LastSeenUtc { get; set; }
        }
    }

    public class WebSocketListener
    {
        private readonly SessionManager sessionManager;
        private readonly MessageRouter router;
        private readonly SessionRegistry sessionRegistry;
        private readonly WebSocketConnectionRegistry connectionRegistry;
        private readonly WebSocketListenerOptions options;
        private readonly ILogger<WebSocketListener> logger;
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public WebSocketListener(
            SessionManager sessionManager,
            MessageRouter router,
            SessionRegistry sessionRegistry,
            WebSocketConnectionRegistry connectionRegistry,
            WebSocketListenerOptions options,
            ILogger<WebSocketListener> logger)
        {
            this.sessionManager = sessionManager;
            this.router = router;
            this.sessionRegistry = sessionRegistry;
            this.connectionRegistry = connectionRegistry;
            this.options = options;
            this.logger = logger;
        }

        public async Task AcceptAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid().ToString("N");
            connectionRegistry.Register(connectionId);
            var connection = new WebSocketSender(socket, logger);

            try
            {
                await ReceiveLoopAsync(connectionId, socket, connection);
            }
            finally
            {
                var sessionId = connectionRegistry.Remove(connectionId);
                if (!string.IsNullOrEmpty(sessionId))
                {
                    sessionRegistry.Remove(sessionId);
                }
            }
        }

        private async Task ReceiveLoopAsync(string connectionId, WebSocket socket, WebSocketSender sender)
        {
            var buffer = new byte[options.ReceiveBufferSize];
            var timeout = options.HeartbeatTimeout;

            while (socket.State == WebSocketState.Open)
            {
                var receiveTask = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var completed = await Task.WhenAny(receiveTask, Task.Delay(timeout));
                if (completed != receiveTask)
                {
                    await SendDisconnectAsync(sender, SystemMessageCodes.HeartbeatTimeout, "Heartbeat timeout.", canReconnect: true);
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Heartbeat timeout", CancellationToken.None);
                    break;
                }

                var result = await receiveTask;
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var payloadBuilder = new StringBuilder();
                payloadBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                while (!result.EndOfMessage)
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    payloadBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }

                var rawJson = payloadBuilder.ToString();
                connectionRegistry.Touch(connectionId);

                if (!TryParseEnvelope(rawJson, sender, out var envelope))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(envelope.SessionId))
                {
                    await sender.SendAsync(new MessageEnvelope<ErrorResponse>
                    {
                        Type = MessageTypes.Error,
                        Payload = new ErrorResponse
                        {
                            Code = SystemMessageCodes.AuthRequired,
                            Message = "Session token required."
                        }
                    });
                    continue;
                }

                if (!sessionManager.TryGetSession(envelope.SessionId, out var session))
                {
                    await SendDisconnectAsync(sender, SystemMessageCodes.AuthExpired, "Session expired.", canReconnect: false);
                    await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Session expired", CancellationToken.None);
                    break;
                }

                sessionManager.TouchSession(envelope.SessionId);
                sessionManager.AttachConnection(envelope.SessionId, connectionId);
                sessionRegistry.AddOrUpdate(envelope.SessionId, session.PlayerId);
                connectionRegistry.BindSession(connectionId, envelope.SessionId);

                if (envelope.Type == MessageTypes.Heartbeat)
                {
                    continue;
                }

                await router.RouteAsync(rawJson, sender);
            }
        }

        private bool TryParseEnvelope(string rawJson, WebSocketSender sender, out MessageEnvelope envelope)
        {
            try
            {
                envelope = JsonSerializer.Deserialize<MessageEnvelope>(rawJson, serializerOptions) ?? new MessageEnvelope();
                return true;
            }
            catch (JsonException)
            {
                _ = sender.SendAsync(new MessageEnvelope<ErrorResponse>
                {
                    Type = MessageTypes.Error,
                    Payload = new ErrorResponse
                    {
                        Code = SystemMessageCodes.BadFormat,
                        Message = "Unrecognized message envelope."
                    }
                });
                envelope = new MessageEnvelope();
                return false;
            }
        }

        private static Task SendDisconnectAsync(WebSocketSender sender, string code, string message, bool canReconnect)
        {
            return sender.SendAsync(new MessageEnvelope<DisconnectNotice>
            {
                Type = MessageTypes.Disconnect,
                Payload = new DisconnectNotice
                {
                    Code = code,
                    Message = message,
                    CanReconnect = canReconnect
                }
            });
        }
    }

    internal sealed class WebSocketSender : IMessageSender
    {
        private readonly WebSocket socket;
        private readonly ILogger logger;
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        private readonly SemaphoreSlim sendLock = new(1, 1);

        public WebSocketSender(WebSocket socket, ILogger logger)
        {
            this.socket = socket;
            this.logger = logger;
        }

        public async Task SendAsync<TPayload>(MessageEnvelope<TPayload> envelope) where TPayload : class
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(envelope, serializerOptions);
            var buffer = Encoding.UTF8.GetBytes(payload);

            await sendLock.WaitAsync();
            try
            {
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send websocket message.");
            }
            finally
            {
                sendLock.Release();
            }
        }
    }
}
