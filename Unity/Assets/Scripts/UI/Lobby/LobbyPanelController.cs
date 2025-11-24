using System.Text;
using Adventure.Net;
using Adventure.Networking;
using Adventure.Shared.Network.Messages;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure.UI.Lobby
{
    /// <summary>
    /// Renders lobby membership and status information while wiring chat to the message pipeline.
    /// </summary>
    public class LobbyPanelController : MonoBehaviour
    {
        [SerializeField]
        private Text lobbyIdLabel;

        [SerializeField]
        private Text lobbyStatusLabel;

        [SerializeField]
        private Text memberListLabel;

        [SerializeField]
        private GameObject emptyMembersState;

        [SerializeField]
        private LobbyHudController lobbyHudController;

        [SerializeField]
        private ClientMessagePipeline messagePipeline;

        [SerializeField]
        private GameStateClient gameStateClient;

        [SerializeField]
        private string fallbackLobbyId = "default";

        private void Awake()
        {
            if (messagePipeline == null)
            {
                messagePipeline = FindObjectOfType<ClientMessagePipeline>();
            }

            if (gameStateClient == null)
            {
                gameStateClient = FindObjectOfType<GameStateClient>();
            }

            BindStateClient();
            InitializeHud();
        }

        private void OnDestroy()
        {
            if (gameStateClient != null)
            {
                gameStateClient.LobbyUpdated -= OnLobbyUpdated;
            }
        }

        public void Initialize(ClientMessagePipeline pipeline, GameStateClient stateClient)
        {
            messagePipeline = pipeline ?? messagePipeline;
            gameStateClient = stateClient ?? gameStateClient;
            BindStateClient();
            InitializeHud();
        }

        public void JoinLobby(string lobbyId)
        {
            var lobbyToJoin = string.IsNullOrWhiteSpace(lobbyId) ? fallbackLobbyId : lobbyId;
            messagePipeline?.JoinLobby(lobbyToJoin);
        }

        public void RenderSnapshot(LobbySnapshot snapshot)
        {
            if (snapshot == null)
            {
                Clear();
                return;
            }

            if (lobbyIdLabel != null)
            {
                lobbyIdLabel.text = string.IsNullOrEmpty(snapshot.LobbyId) ? "Lobby" : snapshot.LobbyId;
            }

            if (lobbyStatusLabel != null)
            {
                lobbyStatusLabel.text = string.IsNullOrEmpty(snapshot.Status) ? "Waiting" : snapshot.Status;
            }

            if (memberListLabel != null)
            {
                memberListLabel.text = BuildMemberList(snapshot);
            }

            if (emptyMembersState != null)
            {
                var hasMembers = snapshot.Members != null && snapshot.Members.Count > 0;
                emptyMembersState.SetActive(!hasMembers);
            }
        }

        public void Clear()
        {
            if (lobbyIdLabel != null)
            {
                lobbyIdLabel.text = "Lobby";
            }

            if (lobbyStatusLabel != null)
            {
                lobbyStatusLabel.text = "Awaiting players";
            }

            if (memberListLabel != null)
            {
                memberListLabel.text = string.Empty;
            }

            if (emptyMembersState != null)
            {
                emptyMembersState.SetActive(true);
            }
        }

        private void InitializeHud()
        {
            if (lobbyHudController != null)
            {
                lobbyHudController.Initialize(gameStateClient, messagePipeline);
            }
        }

        private void BindStateClient()
        {
            if (gameStateClient == null)
            {
                return;
            }

            gameStateClient.LobbyUpdated -= OnLobbyUpdated;
            gameStateClient.LobbyUpdated += OnLobbyUpdated;
        }

        private void OnLobbyUpdated(LobbySnapshot snapshot)
        {
            RenderSnapshot(snapshot);
        }

        private static string BuildMemberList(LobbySnapshot snapshot)
        {
            if (snapshot.Members == null || snapshot.Members.Count == 0)
            {
                return "No players connected";
            }

            var builder = new StringBuilder();
            foreach (var member in snapshot.Members)
            {
                var readyTag = member.IsReady ? "[Ready]" : "[Not Ready]";
                builder.AppendLine($"{member.DisplayName} {readyTag}");
            }

            return builder.ToString();
        }
    }
}
