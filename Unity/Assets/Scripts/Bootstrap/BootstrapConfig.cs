using UnityEngine;

namespace Adventure.Bootstrap
{
    [CreateAssetMenu(fileName = "BootstrapConfig", menuName = "Adventure/Bootstrap Config", order = 0)]
    public class BootstrapConfig : ScriptableObject
    {
        [Header("Network")]
        public string Host = "127.0.0.1";
        public int Port = 7777;

        [Header("Scenes")]
        public string MainMenuScene = "MainMenu";

        [Header("Audio")]
        public AudioMixerBindings AudioBindings;
    }

    [System.Serializable]
    public class AudioMixerBindings
    {
        public AudioListener Listener;
        public AudioSource MusicSource;
        public AudioSource SfxSource;
    }
}
