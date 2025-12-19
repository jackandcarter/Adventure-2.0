using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Adventure.Server.Core.Repositories;
using Adventure.Server.Core.Sessions;
using Adventure.Server.Network;
using Adventure.Shared.Network.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Adventure.Server.Simulation.Tests
{
    public class WebSocketListenerTests
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        [Fact]
        public async Task WebSocketEchoHeartbeatReconnectAndBackpressure()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            builder.Services.AddLogging();
            builder.Services.AddSingleton<SessionRegistry>();
            builder.Services.AddSingleton<MessageRouter>();
            builder.Services.AddSingleton(new WebSocketConnectionRegistry());
            builder.Services.AddSingleton(new WebSocketListenerOptions
            {
                HeartbeatTimeout = TimeSpan.FromMilliseconds(200)
            });
            builder.Services.AddSingleton<SessionManager>(_ =>
                new SessionManager(new InMemoryLoginTokenRepository(), new InMemorySessionRepository()));
            builder.Services.AddSingleton<WebSocketListener>();

            await using var app = builder.Build();
            app.UseWebSockets();
            app.Map("/ws", async (context, listener) => await listener.AcceptAsync(context));

            var router = app.Services.GetRequiredService<MessageRouter>();
            router.RegisterHandler<ChatSendRequest>(MessageTypes.ChatSend, async ctx =>
            {
                var payload = ctx.Payload ?? new ChatSendRequest { Channel = "global", Message = string.Empty };
                await ctx.Sender.SendAsync(new MessageEnvelope<ChatMessage>
                {
                    Type = MessageTypes.ChatBroadcast,
                    SessionId = ctx.Envelope.SessionId,
                    Payload = new ChatMessage
                    {
                        Channel = payload.Channel,
                        Message = payload.Message,
                        Sender = ctx.PlayerId ?? "server"
                    }
                });
            });

            await app.StartAsync();

            var baseAddress = new Uri(app.Urls.First());
            var wsAddress = new Uri($"ws://{baseAddress.Host}:{baseAddress.Port}/ws");
            var sessionManager = app.Services.GetRequiredService<SessionManager>();
            var session = sessionManager.IssueSession("player-echo");

            await using var client = new ClientWebSocket();
            await client.ConnectAsync(wsAddress, CancellationToken.None);

            await SendEnvelopeAsync(client, new MessageEnvelope<Heartbeat>
            {
                Type = MessageTypes.Heartbeat,
                SessionId = session.SessionId,
                Payload = new Heartbeat()
            });

            var messageCount = 12;
            for (var i = 0; i < messageCount; i++)
            {
                await SendEnvelopeAsync(client, new MessageEnvelope<ChatSendRequest>
                {
                    Type = MessageTypes.ChatSend,
                    SessionId = session.SessionId,
                    Payload = new ChatSendRequest
                    {
                        Channel = "global",
                        Message = $"ping-{i}"
                    }
                });
            }

            for (var i = 0; i < messageCount; i++)
            {
                var response = await ReceiveEnvelopeAsync(client);
                Assert.Equal(MessageTypes.ChatBroadcast, response.Type);
            }

            await Task.Delay(300);
            var disconnect = await ReceiveEnvelopeAsync(client);
            Assert.Equal(MessageTypes.Disconnect, disconnect.Type);

            await using var reconnect = new ClientWebSocket();
            await reconnect.ConnectAsync(wsAddress, CancellationToken.None);
            await SendEnvelopeAsync(reconnect, new MessageEnvelope<ChatSendRequest>
            {
                Type = MessageTypes.ChatSend,
                SessionId = session.SessionId,
                Payload = new ChatSendRequest
                {
                    Channel = "global",
                    Message = "reconnect"
                }
            });

            var reconnectResponse = await ReceiveEnvelopeAsync(reconnect);
            Assert.Equal(MessageTypes.ChatBroadcast, reconnectResponse.Type);

            await app.StopAsync();
        }

        private static async Task SendEnvelopeAsync<TPayload>(ClientWebSocket socket, MessageEnvelope<TPayload> envelope)
            where TPayload : class
        {
            var payload = JsonSerializer.Serialize(envelope, SerializerOptions);
            var buffer = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<MessageEnvelope> ReceiveEnvelopeAsync(ClientWebSocket socket)
        {
            var buffer = new byte[4096];
            var builder = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            return JsonSerializer.Deserialize<MessageEnvelope>(builder.ToString(), SerializerOptions) ?? new MessageEnvelope();
        }
    }
}
