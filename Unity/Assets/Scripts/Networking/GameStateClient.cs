using System;
using UnityEngine;

namespace Adventure.Networking
{
    /// <summary>
    /// Tracks client-authoritative state that drives UI (chat, party, player stats, etc.).
    /// </summary>
    public class GameStateClient : MonoBehaviour
    {
        public event Action<string, string> ChatMessageReceived;
        public event Action<PartySnapshot> PartyUpdated;
        public event Action<PlayerVitals> PlayerVitalsUpdated;
        public event Action<AbilityBarState> AbilityBarUpdated;
        public event Action<InteractionPrompt> InteractionPromptUpdated;

        public void PushChatMessage(string sender, string message)
        {
            ChatMessageReceived?.Invoke(sender, message);
        }

        public void PushPartyUpdate(PartySnapshot snapshot)
        {
            PartyUpdated?.Invoke(snapshot);
        }

        public void PushVitals(PlayerVitals vitals)
        {
            PlayerVitalsUpdated?.Invoke(vitals);
        }

        public void PushAbilityBar(AbilityBarState abilityBar)
        {
            AbilityBarUpdated?.Invoke(abilityBar);
        }

        public void PushInteractionPrompt(InteractionPrompt prompt)
        {
            InteractionPromptUpdated?.Invoke(prompt);
        }
    }

    [Serializable]
    public class PartySnapshot
    {
        public PartyMember[] Members = Array.Empty<PartyMember>();
    }

    [Serializable]
    public class PartyMember
    {
        public string Name;
        public int Level;
        public float HealthFraction;
        public bool IsLeader;
    }

    [Serializable]
    public class PlayerVitals
    {
        public int Level;
        public float HealthFraction;
        public float ManaFraction;
        public float ExperienceFraction;
    }

    [Serializable]
    public class AbilityBarState
    {
        public AbilitySlot[] Slots = Array.Empty<AbilitySlot>();
    }

    [Serializable]
    public class AbilitySlot
    {
        public string Name;
        public Sprite Icon;
        public bool OnCooldown;
        public float CooldownFraction;
    }

    [Serializable]
    public class InteractionPrompt
    {
        public string Prompt;
        public bool IsVisible;
    }
}
