using Adventure.Networking;
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

        private NetworkClient networkClient;

        public void Initialize(NetworkClient client)
        {
            networkClient = client;
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
            if (networkClient == null)
            {
                UpdateStatus("Network unavailable");
                return;
            }

            var username = usernameField != null ? usernameField.text : string.Empty;
            var password = passwordField != null ? passwordField.text : string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                UpdateStatus("Missing credentials");
                return;
            }

            networkClient.SendReliable("auth/login", new { user = username, pass = password });
            UpdateStatus("Signing in...");
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
