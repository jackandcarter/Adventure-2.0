using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Adventure.Server.Core.Repositories;

namespace Adventure.Server.Core.Lobby
{
    public record LobbyMemberPresence(string PlayerId, string DisplayName, DateTimeOffset LastSeenUtc, bool IsReady = false);

    public record PartyInvitation(string PartyId, string FromPlayerId, string ToPlayerId, DateTimeOffset SentAtUtc);

    public class PartyState
    {
        public string PartyId { get; }
        public string LeaderId { get; private set; }
        public ConcurrentDictionary<string, LobbyMemberPresence> Members { get; } = new();
        public ConcurrentDictionary<string, PartyInvitation> Invitations { get; } = new();

        public PartyState(string partyId, LobbyMemberPresence leader)
        {
            PartyId = partyId;
            LeaderId = leader.PlayerId;
            Members[leader.PlayerId] = leader;
        }

        public bool ToggleReady(string playerId, bool ready)
        {
            if (!Members.TryGetValue(playerId, out var member))
            {
                return false;
            }

            Members[playerId] = member with { IsReady = ready, LastSeenUtc = DateTimeOffset.UtcNow };
            return true;
        }

        public bool AddMember(LobbyMemberPresence member)
        {
            return Members.TryAdd(member.PlayerId, member);
        }

        public bool RemoveMember(string playerId)
        {
            var removed = Members.TryRemove(playerId, out _);
            if (removed && LeaderId == playerId && Members.Any())
            {
                LeaderId = Members.Values.OrderByDescending(m => m.LastSeenUtc).First().PlayerId;
            }

            return removed;
        }
    }

    public class LobbyChannel
    {
        public string ChannelId { get; }
        public ConcurrentQueue<ChatLogEntry> Messages { get; } = new();

        public LobbyChannel(string channelId)
        {
            ChannelId = channelId;
        }

        public void Append(ChatLogEntry entry)
        {
            Messages.Enqueue(entry);
            while (Messages.Count > 100)
            {
                Messages.TryDequeue(out _);
            }
        }

        public IReadOnlyCollection<ChatLogEntry> Snapshot()
        {
            return Messages.ToArray();
        }
    }

    public record ChatLogEntry(string Channel, string Sender, string Message, DateTimeOffset Timestamp);

    /// <summary>
    /// Coordinates lobby presence, chat channels, and party orchestration.
    /// </summary>
    public class LobbyManager
    {
        private readonly ConcurrentDictionary<string, LobbyMemberPresence> presence = new();
        private readonly ConcurrentDictionary<string, LobbyChannel> channels = new();
        private readonly ConcurrentDictionary<string, PartyState> parties = new();
        private readonly IPlayerProfileRepository playerProfiles;
        private readonly IPartyRepository partyRepository;
        private readonly IChatHistoryRepository chatHistoryRepository;

        public LobbyManager(
            IPlayerProfileRepository playerProfiles,
            IPartyRepository partyRepository,
            IChatHistoryRepository chatHistoryRepository)
        {
            this.playerProfiles = playerProfiles;
            this.partyRepository = partyRepository;
            this.chatHistoryRepository = chatHistoryRepository;

            channels.TryAdd("global", new LobbyChannel("global"));
        }

        public LobbyMemberPresence UpsertPresence(string playerId)
        {
            var profile = playerProfiles.GetProfile(playerId);
            var presenceRecord = new LobbyMemberPresence(playerId, profile.DisplayName, DateTimeOffset.UtcNow);
            presence[playerId] = presenceRecord;
            return presenceRecord;
        }

        public IReadOnlyCollection<LobbyMemberPresence> SnapshotPresence() => presence.Values.ToArray();

        public ChatLogEntry AddChatMessage(string playerId, string channelId, string message)
        {
            var profile = playerProfiles.GetProfile(playerId);
            var entry = new ChatLogEntry(channelId, profile.DisplayName, message, DateTimeOffset.UtcNow);

            var channel = channels.GetOrAdd(channelId, id => new LobbyChannel(id));
            channel.Append(entry);
            chatHistoryRepository.Append(entry);
            return entry;
        }

        public PartyState CreateParty(string leaderId)
        {
            var leaderPresence = UpsertPresence(leaderId);
            var partyId = Guid.NewGuid().ToString("N");
            var party = new PartyState(partyId, leaderPresence);
            parties[partyId] = party;
            partyRepository.SaveParty(party);
            return party;
        }

        public bool InviteToParty(string partyId, string fromPlayerId, string toPlayerId)
        {
            if (!parties.TryGetValue(partyId, out var party) || party.LeaderId != fromPlayerId)
            {
                return false;
            }

            var invitation = new PartyInvitation(partyId, fromPlayerId, toPlayerId, DateTimeOffset.UtcNow);
            party.Invitations[toPlayerId] = invitation;
            partyRepository.SaveParty(party);
            return true;
        }

        public bool AcceptInvite(string toPlayerId, string partyId)
        {
            if (!parties.TryGetValue(partyId, out var party))
            {
                return false;
            }

            if (!party.Invitations.TryRemove(toPlayerId, out _))
            {
                return false;
            }

            var presenceRecord = UpsertPresence(toPlayerId);
            var added = party.AddMember(presenceRecord);
            if (added)
            {
                partyRepository.SaveParty(party);
            }

            return added;
        }

        public bool ToggleReady(string partyId, string playerId, bool ready)
        {
            if (!parties.TryGetValue(partyId, out var party))
            {
                return false;
            }

            var changed = party.ToggleReady(playerId, ready);
            if (changed)
            {
                partyRepository.SaveParty(party);
            }

            return changed;
        }

        public bool LeaveParty(string partyId, string playerId)
        {
            if (!parties.TryGetValue(partyId, out var party))
            {
                return false;
            }

            var removed = party.RemoveMember(playerId);
            if (removed)
            {
                partyRepository.SaveParty(party);
                if (!party.Members.Any())
                {
                    parties.TryRemove(partyId, out _);
                    partyRepository.DeleteParty(partyId);
                }
            }

            return removed;
        }

        public IReadOnlyCollection<ChatLogEntry> GetRecentMessages(string channelId)
        {
            var channel = channels.GetOrAdd(channelId, id => new LobbyChannel(id));
            return channel.Snapshot();
        }

        public IReadOnlyCollection<PartyState> GetParties() => parties.Values.ToArray();
    }
}
