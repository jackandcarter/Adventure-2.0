using System.Collections.Generic;
using Adventure.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure.UI.MainMenu
{
    /// <summary>
    /// Binds to existing UI elements to display character options and request game entry.
    /// </summary>
    public class CharacterSelectController : MonoBehaviour
    {
        [SerializeField]
        private Dropdown characterDropdown;

        [SerializeField]
        private Button playButton;

        [SerializeField]
        private Text statusLabel;

        private readonly List<string> characters = new();
        private NetworkClient networkClient;

        public void Initialize(NetworkClient client, IEnumerable<string> availableCharacters)
        {
            networkClient = client;
            characters.Clear();
            characters.AddRange(availableCharacters);
            RefreshDropdown();
            WireUi();
        }

        private void RefreshDropdown()
        {
            if (characterDropdown == null)
            {
                return;
            }

            characterDropdown.ClearOptions();
            characterDropdown.AddOptions(characters);
        }

        private void WireUi()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayClicked);
                playButton.onClick.AddListener(OnPlayClicked);
            }
        }

        private void OnPlayClicked()
        {
            if (networkClient == null)
            {
                UpdateStatus("Network unavailable");
                return;
            }

            if (characters.Count == 0)
            {
                UpdateStatus("No characters found");
                return;
            }

            var index = characterDropdown != null ? characterDropdown.value : 0;
            index = Mathf.Clamp(index, 0, characters.Count - 1);
            var characterName = characters[index];
            networkClient.SendReliable("character/select", new { name = characterName });
            UpdateStatus($"Loading {characterName}...");
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }
    }
}
