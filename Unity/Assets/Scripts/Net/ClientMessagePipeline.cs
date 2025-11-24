using System;
using System.Collections.Generic;
using System.Text.Json;
using Adventure.Networking;
using Adventure.Shared.Network.Messages;
using Adventure.UI.Notification;
using UnityEngine;

namespace Adventure.Net
{
    /// <summary>
    /// Client-side message pump that buffers outgoing sends, processes inbound envelopes, and
    /// drives keep-alive / reconnection behavior for lightweight transports.
    /// </summary>
    public class ClientMessagePipeline : MonoBehaviour
    {
        [SerializeField]
        private NetworkClient transport;

        [SerializeField]
        private GameStateClient gameStateClient;

        [SerializeField]
        private string clientVersion = "0.1.0";

        [SerializeField]
        private float heartbeatIntervalSeconds = 8f;

        [SerializeField]
        private float reconnectDelaySeconds = 2f;

        [SerializeField]
        private int sendBudgetPerFrame = 4;

        [SerializeField]
        private NotificationsPresenter notificationsPresenter;

        private readonly Queue<MessageEnvelope> outbound = new();
        private readonly Queue<MessageEnvelope> inbound = new();

        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private float lastSendTime;
        private float lastReceiveTime;
        private bool reconnectQueued;

        private string sessionId = string.Empty;

        public event Action<AuthResponse> AuthResponseReceived;
        public event Action<LobbySnapshot> LobbyUpdated;

        private void Awake()
        {
            if (transport == null)
            {
                transport = FindObjectOfType<NetworkClient>();
            }

            if (gameStateClient == null)
            {
                gameStateClient = FindObjectOfType<GameStateClient>();
            }

            if (transport != null)
            {
                transport.Connected += OnConnected;
                transport.Disconnected += OnDisconnected;
                transport.MessageReceived += OnMessageReceived;
            }
        }

        private void OnDestroy()
        {
            if (transport != null)
            {
                transport.Connected -= OnConnected;
                transport.Disconnected -= OnDisconnected;
                transport.MessageReceived -= OnMessageReceived;
            }
        }

        private void Update()
        {
            FlushOutbound();
            ProcessInbound();
            PumpHeartbeat();
            TryReconnect();
        }

        public void EnqueueSend<TPayload>(string type, string sessionId, TPayload payload) where TPayload : class
        {
            outbound.Enqueue(new MessageEnvelope<TPayload>
            {
                Type = type,
                SessionId = sessionId,
                Payload = payload
            });
        }

        public void SendAuthRequest(string username, string password, string versionOverride = "")
        {
            var version = string.IsNullOrWhiteSpace(versionOverride) ? clientVersion : versionOverride;
            EnqueueSend(MessageTypes.AuthRequest, string.Empty, new AuthRequest
            {
                UserName = username,
                Password = password,
                ClientVersion = version
            });
        }

        public void JoinLobby(string lobbyId)
        {
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                return;
            }

            EnqueueSend(MessageTypes.LobbyJoin, sessionId, new LobbyJoinRequest
            {
                LobbyId = lobbyId
            });
        }

        public void SendChatMessage(string message, string channel = "global")
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            EnqueueSend(MessageTypes.ChatSend, sessionId, new ChatSendRequest
            {
                Channel = string.IsNullOrWhiteSpace(channel) ? "global" : channel,
                Message = message
            });
        }

        public void ReceiveRaw(string rawJson)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<MessageEnvelope>(rawJson, serializerOptions);
                if (envelope != null)
                {
                    inbound.Enqueue(envelope);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to parse inbound message: {ex.Message}\n{rawJson}");
            }
        }

        private void FlushOutbound()
        {
            if (transport == null || !transport.IsConnected)
            {
                return;
            }

            for (int i = 0; i < sendBudgetPerFrame && outbound.Count > 0; i++)
            {
                var envelope = outbound.Dequeue();
                if (string.IsNullOrEmpty(envelope.SessionId) && !string.IsNullOrEmpty(sessionId))
                {
                    envelope.SessionId = sessionId;
                }

                switch (envelope)
                {
                    case MessageEnvelope<ChatSendRequest> chatSend:
                        transport.SendEnvelope(chatSend);
                        break;
                    case MessageEnvelope<Heartbeat> heartbeat:
                        transport.SendEnvelope(heartbeat);
                        break;
                    case MessageEnvelope<MovementCommand> movement:
                        transport.SendEnvelope(movement);
                        break;
                    case MessageEnvelope<AbilityCastRequest> abilityCast:
                        transport.SendEnvelope(abilityCast);
                        break;
                    default:
                        transport.SendEnvelope(new MessageEnvelope<object>
                        {
                            Type = envelope.Type,
                            Payload = envelope.Payload,
                            SessionId = envelope.SessionId,
                            RequestId = envelope.RequestId
                        });
                        break;
                }

                lastSendTime = Time.time;
            }
        }

        private void ProcessInbound()
        {
            while (inbound.Count > 0)
            {
                var envelope = inbound.Dequeue();
                switch (envelope.Type)
                {
                    case MessageTypes.AuthResponse:
                        HandleAuthResponse(envelope.Payload);
                        break;
                    case MessageTypes.ChatBroadcast:
                        HandleChatBroadcast(envelope.Payload);
                        break;
                    case MessageTypes.LobbyUpdate:
                        HandleLobbyUpdate(envelope.Payload);
                        break;
                    case MessageTypes.PartyUpdate:
                        HandlePartyUpdate(envelope.Payload);
                        break;
                    case MessageTypes.CombatEvent:
                        HandleCombatEvent(envelope.Payload);
                        break;
                    case MessageTypes.DungeonState:
                        HandleDungeonState(envelope.Payload);
                        break;
                    case MessageTypes.Error:
                        HandleError(envelope.Payload);
                        break;
                }

                lastReceiveTime = Time.time;
            }
        }

        private void PumpHeartbeat()
        {
            if (transport == null || !transport.IsConnected)
            {
                return;
            }

            if (Time.time - lastSendTime > heartbeatIntervalSeconds)
            {
                EnqueueSend(MessageTypes.Heartbeat, string.Empty, new Heartbeat());
            }
        }

        private void TryReconnect()
        {
            if (!reconnectQueued || transport == null)
            {
                return;
            }

            if (Time.time - lastReceiveTime < reconnectDelaySeconds)
            {
                return;
            }

            reconnectQueued = false;
            _ = transport.ConnectAsync();
        }

        private void OnConnected()
        {
            reconnectQueued = false;
            EnqueueSend(MessageTypes.Heartbeat, string.Empty, new Heartbeat());
        }

        private void OnDisconnected(string reason)
        {
            sessionId = string.Empty;
            reconnectQueued = true;
            ShowNotification($"Connection lost: {reason}. Attempting to reconnect...");
        }

        private void OnMessageReceived(string rawJson)
        {
            ReceiveRaw(rawJson);
        }

        private void HandleChatBroadcast(object payload)
        {
            var chatMessage = ConvertPayload<ChatMessage>(payload);
            if (chatMessage != null && gameStateClient != null)
            {
                gameStateClient.PushChatMessage(chatMessage.Sender, chatMessage.Message);
            }
        }

        private void HandleAuthResponse(object payload)
        {
            var authResponse = ConvertPayload<AuthResponse>(payload);
            if (authResponse == null)
            {
                return;
            }

            if (authResponse.Success && !string.IsNullOrEmpty(authResponse.SessionId))
            {
                sessionId = authResponse.SessionId;
            }

            AuthResponseReceived?.Invoke(authResponse);
        }

        private void HandleLobbyUpdate(object payload)
        {
            var lobbySnapshot = ConvertPayload<LobbySnapshot>(payload);
            if (lobbySnapshot == null)
            {
                return;
            }

            gameStateClient?.PushLobbyUpdate(lobbySnapshot);
            LobbyUpdated?.Invoke(lobbySnapshot);
        }

        private void HandlePartyUpdate(object payload)
        {
            var partyState = ConvertPayload<PartyState>(payload);
            if (partyState == null || gameStateClient == null)
            {
                return;
            }

            var snapshot = new PartySnapshot
            {
                Members = CreatePartyMembers(partyState.Members)
            };

            gameStateClient.PushPartyUpdate(snapshot);
        }

        private void HandleCombatEvent(object payload)
        {
            var combatEvent = ConvertPayload<CombatEvent>(payload);
            if (combatEvent != null && gameStateClient != null)
            {
                gameStateClient.PushCombatEvent(combatEvent);
                if (combatEvent.EventType == CombatEventType.Damage)
                {
                    ShowNotification($"{combatEvent.SourceId} hit {combatEvent.TargetId} for {combatEvent.Amount}");
                }
                else
                {
                    ShowNotification($"{combatEvent.TargetId} healed for {combatEvent.Amount}");
                }
            }
        }

        private void HandleDungeonState(object payload)
        {
            var dungeonState = ConvertPayload<DungeonState>(payload);
            if (dungeonState != null && gameStateClient != null)
            {
                gameStateClient.PushDungeonState(dungeonState);
            }
        }

        private void HandleError(object payload)
        {
            var error = ConvertPayload<ErrorResponse>(payload) ?? new ErrorResponse
            {
                Code = "unknown",
                Message = payload?.ToString() ?? "Unknown error"
            };

            gameStateClient?.PushError(error);
            ShowNotification($"Action denied: {error.Message}");
        }

        private static PartyMember[] CreatePartyMembers(List<Adventure.Shared.Network.Messages.PartyMember> members)
        {
            if (members == null)
            {
                return Array.Empty<PartyMember>();
            }

            var converted = new List<PartyMember>();
            foreach (var member in members)
            {
                converted.Add(new PartyMember
                {
                    Name = member.DisplayName,
                    Level = member.Level,
                    HealthFraction = member.HealthFraction,
                    IsLeader = member.Leader
                });
            }

            return converted.ToArray();
        }

        private TPayload? ConvertPayload<TPayload>(object payload) where TPayload : class
        {
            if (payload is TPayload typed)
            {
                return typed;
            }

            if (payload is JsonElement json)
            {
                try
                {
                    return JsonSerializer.Deserialize<TPayload>(json.GetRawText(), serializerOptions);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to convert payload to {typeof(TPayload).Name}: {ex.Message}");
                }
            }

            return null;
        }

        private void ShowNotification(string message)
        {
            if (notificationsPresenter != null)
            {
                notificationsPresenter.Show(message);
            }
        }
    }
}
