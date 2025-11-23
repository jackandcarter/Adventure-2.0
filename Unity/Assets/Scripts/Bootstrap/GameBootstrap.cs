using System.Collections;
using Adventure.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Adventure.Bootstrap
{
    /// <summary>
    /// Entry point that prepares network/audio clients and transitions into the main menu.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private BootstrapConfig config;

        [SerializeField]
        private NetworkClient networkClient;

        [SerializeField]
        private GameStateClient gameStateClient;

        [SerializeField]
        private AudioListener audioListener;

        [SerializeField]
        private AudioSource musicSource;

        [SerializeField]
        private AudioSource sfxSource;

        [SerializeField]
        private bool autoStart = true;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("Bootstrap config missing, cannot continue.");
                return;
            }

            BindAudio(config.AudioBindings);
            networkClient.Configure(config.Host, config.Port);
        }

        private void Start()
        {
            if (autoStart)
            {
                StartCoroutine(BootstrapRoutine());
            }
        }

        private IEnumerator BootstrapRoutine()
        {
            yield return networkClient.ConnectAsync().AsIEnumerator();
            // No runtime-created canvases; controllers rely on scene-assigned references only.
            SceneManager.LoadScene(config.MainMenuScene, LoadSceneMode.Single);
        }

        private void BindAudio(AudioMixerBindings bindings)
        {
            if (bindings == null)
            {
                Debug.LogWarning("Audio bindings missing; audio will remain default.");
                return;
            }

            if (audioListener == null && bindings.Listener != null)
            {
                audioListener = bindings.Listener;
            }

            if (musicSource == null && bindings.MusicSource != null)
            {
                musicSource = bindings.MusicSource;
            }

            if (sfxSource == null && bindings.SfxSource != null)
            {
                sfxSource = bindings.SfxSource;
            }
        }
    }

    public static class TaskExtensions
    {
        public static IEnumerator AsIEnumerator(this System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }
    }
}
