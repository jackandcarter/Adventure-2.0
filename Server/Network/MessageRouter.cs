using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Adventure.Shared.Network.Messages;

namespace Adventure.Server.Network
{
    public interface IMessageSender
    {
        Task SendAsync<TPayload>(MessageEnvelope<TPayload> envelope) where TPayload : class;
    }

    public class SessionRegistry
    {
        private readonly ConcurrentDictionary<string, string> activeSessions = new();

        public void AddOrUpdate(string sessionId, string playerId)
        {
            activeSessions[sessionId] = playerId;
        }

        public bool TryGetPlayerId(string sessionId, out string playerId)
        {
            return activeSessions.TryGetValue(sessionId, out playerId!);
        }

        public void Remove(string sessionId)
        {
            activeSessions.TryRemove(sessionId, out _);
        }
    }

    public class MessageContext
    {
        public MessageEnvelope Envelope { get; init; } = new();

        public string? PlayerId { get; init; }

        public IMessageSender Sender { get; init; } = default!;
    }

    public class MessageContext<TPayload> : MessageContext where TPayload : class
    {
        public TPayload? Payload { get; init; }
    }

    /// <summary>
    /// Lightweight routing layer that deserializes envelopes, checks session validity, and dispatches handlers.
    /// </summary>
    public class MessageRouter
    {
        private readonly Dictionary<string, Func<MessageContext, Task>> handlers = new();
        private readonly SessionRegistry sessions;
        private readonly JsonSerializerOptions serializerOptions;

        public MessageRouter(SessionRegistry sessions)
        {
            this.sessions = sessions;
            serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public void RegisterHandler<TPayload>(string messageType, Func<MessageContext<TPayload>, Task> handler) where TPayload : class
        {
            handlers[messageType] = async ctx =>
            {
                var payload = ctx.Envelope.Payload is JsonElement jsonElement
                    ? JsonSerializer.Deserialize<TPayload>(jsonElement.GetRawText(), serializerOptions)
                    : ctx.Envelope.Payload as TPayload;

                await handler(new MessageContext<TPayload>
                {
                    Envelope = ctx.Envelope,
                    PlayerId = ctx.PlayerId,
                    Sender = ctx.Sender,
                    Payload = payload
                });
            };
        }

        public async Task RouteAsync(string rawJson, IMessageSender sender)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return;
            }

            MessageEnvelope? envelope = null;
            try
            {
                envelope = JsonSerializer.Deserialize<MessageEnvelope>(rawJson, serializerOptions);
            }
            catch (JsonException)
            {
                await sender.SendAsync(new MessageEnvelope<ErrorResponse>
                {
                    Type = MessageTypes.Error,
                    Payload = new ErrorResponse { Code = SystemMessageCodes.BadFormat, Message = "Unrecognized message envelope." }
                });
                return;
            }

            if (envelope == null)
            {
                return;
            }

            if (!sessions.TryGetPlayerId(envelope.SessionId, out var playerId))
            {
                await sender.SendAsync(new MessageEnvelope<ErrorResponse>
                {
                    Type = MessageTypes.Error,
                    Payload = new ErrorResponse { Code = SystemMessageCodes.InvalidSession, Message = "Session expired or unknown.", RetryAfterSeconds = 1 }
                });
                return;
            }

            if (handlers.TryGetValue(envelope.Type, out var handler))
            {
                await handler(new MessageContext
                {
                    Envelope = envelope,
                    PlayerId = playerId,
                    Sender = sender
                });
            }
            else
            {
                await sender.SendAsync(new MessageEnvelope<ErrorResponse>
                {
                    Type = MessageTypes.Error,
                    Payload = new ErrorResponse { Code = SystemMessageCodes.UnknownType, Message = $"No handler for {envelope.Type}" }
                });
            }
        }
    }
}
