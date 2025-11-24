using System.Collections.Generic;
using Adventure.Net;
using Adventure.Networking;
using Adventure.Shared.Network.Messages;
using Adventure.UI.Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure.UI.MainMenu
{
    /// <summary>
    /// High-level controller that wires login, lobby, and character selection views to the shared network stack.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private enum MenuState
        {
            Login,
            Lobby,
            CharacterSelect
        }

        [SerializeField]
        private LoginPanelController loginPanel;

        [SerializeField]
        private CharacterSelectController characterSelectPanel;

        [SerializeField]
        private LobbyPanelController lobbyPanel;

        [SerializeField]
        private NetworkClient networkClient;

        [SerializeField]
        private ClientMessagePipeline messagePipeline;

        [SerializeField]
        private GameStateClient gameStateClient;

        [SerializeField]
        private GameObject loginRoot;

        [SerializeField]
        private GameObject lobbyRoot;

        [SerializeField]
        private GameObject characterRoot;

        [SerializeField]
        private Text connectionStatusLabel;

        [SerializeField]
        private string defaultLobbyId = "default";

        [SerializeField]
        private List<string> mockCharacters = new() { "Rogue", "Mage", "Knight" };

        private MenuState currentState = MenuState.Login;

        private void Awake()
        {
            if (networkClient == null)
            {
                networkClient = FindObjectOfType<NetworkClient>();
            }

            if (messagePipeline == null)
            {
                messagePipeline = FindObjectOfType<ClientMessagePipeline>();
            }

            if (gameStateClient == null)
            {
                gameStateClient = FindObjectOfType<GameStateClient>();
            }

            loginPanel?.Initialize(messagePipeline);
            characterSelectPanel?.Initialize(networkClient, mockCharacters);
            lobbyPanel?.Initialize(messagePipeline, gameStateClient);

            SetState(MenuState.Login);
            UpdateConnectionStatus("Not connected");
            loginPanel?.SetInteractable(messagePipeline != null);
        }

        private void OnEnable()
        {
            if (loginPanel != null)
            {
                loginPanel.LoginSubmitted += OnLoginSubmitted;
            }

            if (messagePipeline != null)
            {
                messagePipeline.AuthResponseReceived += OnAuthResponse;
                messagePipeline.LobbyUpdated += OnLobbyUpdated;
            }

            if (networkClient != null)
            {
                networkClient.Connected += OnConnected;
                networkClient.Disconnected += OnDisconnected;
            }
        }

        private void OnDisable()
        {
            if (loginPanel != null)
            {
                loginPanel.LoginSubmitted -= OnLoginSubmitted;
            }

            if (messagePipeline != null)
            {
                messagePipeline.AuthResponseReceived -= OnAuthResponse;
                messagePipeline.LobbyUpdated -= OnLobbyUpdated;
            }

            if (networkClient != null)
            {
                networkClient.Connected -= OnConnected;
                networkClient.Disconnected -= OnDisconnected;
            }
        }

        private void OnLoginSubmitted(string username, string password)
        {
            UpdateConnectionStatus("Signing in...");
        }

        private void OnAuthResponse(AuthResponse response)
        {
            if (response.Success)
            {
                loginPanel?.ShowSuccess("Login successful");
                lobbyPanel?.JoinLobby(defaultLobbyId);
                SetState(MenuState.Lobby);
                UpdateConnectionStatus("Authenticated");
            }
            else
            {
                loginPanel?.ShowError($"Login failed: {response.DenialReason}");
                SetState(MenuState.Login);
                UpdateConnectionStatus("Login failed");
            }
        }

        private void OnLobbyUpdated(LobbySnapshot snapshot)
        {
            lobbyPanel?.RenderSnapshot(snapshot);
            SetState(MenuState.Lobby);
            UpdateConnectionStatus("In lobby");
        }

        public void ShowCharacterSelection()
        {
            SetState(MenuState.CharacterSelect);
            UpdateConnectionStatus("Select your character");
        }

        private void OnConnected()
        {
            UpdateConnectionStatus("Connected to server");
            loginPanel?.SetStatus("Connected. Please log in.");
            loginPanel?.SetInteractable(true);
        }

        private void OnDisconnected(string reason)
        {
            UpdateConnectionStatus($"Disconnected: {reason}");
            loginPanel?.SetStatus("Connection lost. Please try again.");
            loginPanel?.SetInteractable(true);
            SetState(MenuState.Login);
            lobbyPanel?.Clear();
        }

        private void SetState(MenuState state)
        {
            currentState = state;
            TogglePanels(state);
        }

        private void TogglePanels(MenuState state)
        {
            if (loginRoot != null)
            {
                loginRoot.SetActive(state == MenuState.Login);
            }

            if (lobbyRoot != null)
            {
                lobbyRoot.SetActive(state == MenuState.Lobby);
            }

            if (characterRoot != null)
            {
                characterRoot.SetActive(state == MenuState.CharacterSelect);
            }
        }

        private void UpdateConnectionStatus(string message)
        {
            if (connectionStatusLabel != null)
            {
                connectionStatusLabel.text = message;
            }
        }
    }
}
