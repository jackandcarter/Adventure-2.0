using System.Text;
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

        private readonly StringBuilder chatBuffer = new();

        private void Awake()
        {
            if (gameStateClient == null)
            {
                gameStateClient = FindObjectOfType<GameStateClient>();
            }

            if (gameStateClient != null)
            {
                gameStateClient.ChatMessageReceived += OnChatMessage;
                gameStateClient.PartyUpdated += OnPartyUpdated;
            }
        }

        private void OnDestroy()
        {
            if (gameStateClient != null)
            {
                gameStateClient.ChatMessageReceived -= OnChatMessage;
                gameStateClient.PartyUpdated -= OnPartyUpdated;
            }
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
    }
}
