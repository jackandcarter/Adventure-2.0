using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Adventure.Server.Persistence;

namespace Adventure.Server.Core.Repositories
{
    public class AccountProfileRepository : IPlayerProfileRepository
    {
        private readonly IAccountRepository accounts;

        public AccountProfileRepository(IAccountRepository accounts)
        {
            this.accounts = accounts;
        }

        public PlayerProfile GetProfile(string playerId)
        {
            var account = accounts.GetById(playerId);
            if (account == null)
            {
                throw new InvalidOperationException($"Account {playerId} was not found.");
            }

            return new PlayerProfile(account.AccountId, account.DisplayName, 1);
        }
    }

    public class DungeonRunRepositoryAdapter : IDungeonRunRepository
    {
        private readonly Adventure.Server.Persistence.IDungeonRunRepository persistence;

        public DungeonRunRepositoryAdapter(Adventure.Server.Persistence.IDungeonRunRepository persistence)
        {
            this.persistence = persistence;
        }

        public void RecordEnd(string instanceId)
        {
            persistence.CompleteRun(instanceId, "completed", DateTime.UtcNow);
        }

        public void RecordStart(string instanceId, string dungeonId, string partyId)
        {
            persistence.BeginRun(dungeonId, partyId, DateTime.UtcNow);
        }
    }

    public class InMemoryLoginTokenRepository : ILoginTokenRepository
    {
        private readonly ConcurrentDictionary<string, string> tokens = new();

        public string IssueToken(string playerId)
        {
            var token = Guid.NewGuid().ToString("N");
            tokens[token] = playerId;
            return token;
        }

        public bool ValidateToken(string token, out string playerId)
        {
            return tokens.TryGetValue(token, out playerId!);
        }
    }

    public class InMemorySessionRepository : ISessionRepository
    {
        private readonly ConcurrentDictionary<string, string> sessions = new();

        public void PersistSession(string sessionId, string playerId)
        {
            sessions[sessionId] = playerId;
        }

        public void RemoveSession(string sessionId)
        {
            sessions.TryRemove(sessionId, out _);
        }
    }

    public class InMemoryPartyRepository : IPartyRepository
    {
        private readonly ConcurrentDictionary<string, Lobby.PartyState> parties = new();

        public void DeleteParty(string partyId)
        {
            parties.TryRemove(partyId, out _);
        }

        public Lobby.PartyState? GetParty(string partyId)
        {
            parties.TryGetValue(partyId, out var party);
            return party;
        }

        public void SaveParty(Lobby.PartyState party)
        {
            parties[party.PartyId] = party;
        }
    }

    public class InMemoryChatHistoryRepository : IChatHistoryRepository
    {
        private readonly ConcurrentDictionary<string, List<ChatLogEntry>> history = new();

        public void Append(ChatLogEntry entry)
        {
            var list = history.GetOrAdd(entry.Channel, _ => new List<ChatLogEntry>());
            lock (list)
            {
                list.Add(entry);
                while (list.Count > 250)
                {
                    list.RemoveAt(0);
                }
            }
        }

        public IReadOnlyCollection<ChatLogEntry> Recent(string channelId, int limit = 100)
        {
            if (!history.TryGetValue(channelId, out var list))
            {
                return Array.Empty<ChatLogEntry>();
            }

            lock (list)
            {
                return list.TakeLast(limit).ToArray();
            }
        }
    }
}
