using System;
using Adventure.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Adventure.UI.MainMenu
{
    /// <summary>
    /// Binds to existing login fields/buttons and relays credentials to the server via NetworkClient.
    /// </summary>
    public class LoginPanelController : MonoBehaviour
    {
        [SerializeField]
        private InputField usernameField;

        [SerializeField]
        private InputField passwordField;

        [SerializeField]
        private Button submitButton;

        [SerializeField]
        private Text statusLabel;

        [SerializeField]
        private ClientMessagePipeline messagePipeline;

        [SerializeField]
        private string clientVersion = "0.1.0";

        public event Action<string, string> LoginSubmitted;

        public void Initialize(ClientMessagePipeline pipeline)
        {
            messagePipeline = pipeline;
            WireUi();
        }

        private void Awake()
        {
            if (messagePipeline == null)
            {
                messagePipeline = FindObjectOfType<ClientMessagePipeline>();
            }

            WireUi();
        }

        private void WireUi()
        {
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(OnSubmit);
                submitButton.onClick.AddListener(OnSubmit);
            }
        }

        private void OnSubmit()
        {
            var username = usernameField != null ? usernameField.text : string.Empty;
            var password = passwordField != null ? passwordField.text : string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                UpdateStatus("Missing credentials");
                return;
            }

            UpdateStatus("Signing in...");
            SetInteractable(false);

            if (messagePipeline != null)
            {
                messagePipeline.SendAuthRequest(username, password, clientVersion);
            }
            else
            {
                UpdateStatus("Network unavailable");
                SetInteractable(true);
            }

            LoginSubmitted?.Invoke(username, password);
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        public void SetStatus(string message)
        {
            UpdateStatus(message);
        }

        public void SetInteractable(bool enabled)
        {
            if (usernameField != null)
            {
                usernameField.interactable = enabled;
            }

            if (passwordField != null)
            {
                passwordField.interactable = enabled;
            }

            if (submitButton != null)
            {
                submitButton.interactable = enabled;
            }
        }

        public void ShowError(string message)
        {
            UpdateStatus(message);
            SetInteractable(true);
        }

        public void ShowSuccess(string message)
        {
            UpdateStatus(message);
        }
    }
}
