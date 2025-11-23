using System.Collections.Generic;
using Adventure.Networking;
using UnityEngine;

namespace Adventure.UI.MainMenu
{
    /// <summary>
    /// High-level controller that wires login and character selection views to a shared NetworkClient.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField]
        private LoginPanelController loginPanel;

        [SerializeField]
        private CharacterSelectController characterSelectPanel;

        [SerializeField]
        private NetworkClient networkClient;

        [SerializeField]
        private GameObject loginRoot;

        [SerializeField]
        private GameObject characterRoot;

        [SerializeField]
        private List<string> mockCharacters = new() { "Rogue", "Mage", "Knight" };

        private void Awake()
        {
            if (networkClient == null)
            {
                networkClient = FindObjectOfType<NetworkClient>();
            }

            if (loginPanel != null && networkClient != null)
            {
                loginPanel.Initialize(networkClient);
            }

            if (characterSelectPanel != null && networkClient != null)
            {
                characterSelectPanel.Initialize(networkClient, mockCharacters);
            }

            TogglePanels(loginRootActive: true);
        }

        public void ShowCharacterSelection()
        {
            TogglePanels(loginRootActive: false);
        }

        private void TogglePanels(bool loginRootActive)
        {
            if (loginRoot != null)
            {
                loginRoot.SetActive(loginRootActive);
            }

            if (characterRoot != null)
            {
                characterRoot.SetActive(!loginRootActive);
            }
        }
    }
}
