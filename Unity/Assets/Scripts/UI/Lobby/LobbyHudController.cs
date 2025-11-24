using System.Text;
using Adventure.Net;
using Adventure.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure.UI.Lobby
{
    /// <summary>
    /// Subscribes to lobby chat and party notifications while toggling existing panels.
    /// </summary>
    public class LobbyHudController : MonoBehaviour
    {
        [SerializeField]
        private GameStateClient gameStateClient;

        [SerializeField]
        private GameObject chatPanel;

        [SerializeField]
        private GameObject partyPanel;

        [SerializeField]
        private Text chatLog;

        [SerializeField]
        private Text partySummary;

        [SerializeField]
        private InputField chatInputField;

        [SerializeField]
        private Button sendChatButton;

        [SerializeField]
        private string chatChannel = "global";

        [SerializeField]
        private ClientMessagePipeline messagePipeline;

        private readonly StringBuilder chatBuffer = new();
        private bool subscriptionsActive;

        private void Awake()
        {
            if (gameStateClient == null)
            {
                gameStateClient = FindObjectOfType<GameStateClient>();
            }

            if (messagePipeline == null)
            {
                messagePipeline = FindObjectOfType<ClientMessagePipeline>();
            }

            EnsureSubscriptions();
            WireChatInput();
        }

        private void OnDestroy()
        {
            if (subscriptionsActive && gameStateClient != null)
            {
                gameStateClient.ChatMessageReceived -= OnChatMessage;
                gameStateClient.PartyUpdated -= OnPartyUpdated;
                subscriptionsActive = false;
            }

            UnwireChatInput();
        }

        public void Initialize(GameStateClient stateClient, ClientMessagePipeline pipeline = null)
        {
            gameStateClient = stateClient ?? gameStateClient;
            messagePipeline = pipeline ?? messagePipeline;
            EnsureSubscriptions();
            WireChatInput();
        }

        private void OnChatMessage(string sender, string message)
        {
            chatBuffer.AppendLine($"[{sender}] {message}");
            if (chatLog != null)
            {
                chatLog.text = chatBuffer.ToString();
            }

            TogglePanel(chatPanel, true);
        }

        private void OnPartyUpdated(PartySnapshot snapshot)
        {
            if (partySummary != null && snapshot != null && snapshot.Members != null)
            {
                var builder = new StringBuilder();
                foreach (var member in snapshot.Members)
                {
                    builder.AppendLine($"{member.Name} (Lv {member.Level}) {(member.IsLeader ? "[Leader]" : string.Empty)}");
                }

                partySummary.text = builder.ToString();
            }

            TogglePanel(partyPanel, snapshot != null && snapshot.Members.Length > 0);
        }

        private void TogglePanel(GameObject panel, bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }

        private void WireChatInput()
        {
            if (sendChatButton != null)
            {
                sendChatButton.onClick.RemoveListener(OnSendChatClicked);
                sendChatButton.onClick.AddListener(OnSendChatClicked);
            }

            if (chatInputField != null)
            {
                chatInputField.onEndEdit.RemoveListener(OnChatEditEnded);
                chatInputField.onEndEdit.AddListener(OnChatEditEnded);
            }
        }

        private void UnwireChatInput()
        {
            if (sendChatButton != null)
            {
                sendChatButton.onClick.RemoveListener(OnSendChatClicked);
            }

            if (chatInputField != null)
            {
                chatInputField.onEndEdit.RemoveListener(OnChatEditEnded);
            }
        }

        private void OnSendChatClicked()
        {
            SendChatFromInput();
        }

        private void OnChatEditEnded(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                SendChatFromInput();
            }
        }

        private void SendChatFromInput()
        {
            var message = chatInputField != null ? chatInputField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (messagePipeline != null)
            {
                messagePipeline.SendChatMessage(message, chatChannel);
            }

            if (chatInputField != null)
            {
                chatInputField.text = string.Empty;
            }
        }

        public void SetChatInputEnabled(bool enabled)
        {
            if (chatInputField != null)
            {
                chatInputField.interactable = enabled;
            }

            if (sendChatButton != null)
            {
                sendChatButton.interactable = enabled;
            }
        }

        private void EnsureSubscriptions()
        {
            if (!subscriptionsActive && gameStateClient != null)
            {
                gameStateClient.ChatMessageReceived += OnChatMessage;
                gameStateClient.PartyUpdated += OnPartyUpdated;
                subscriptionsActive = true;
            }
        }
    }
}
