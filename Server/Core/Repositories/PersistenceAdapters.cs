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

        public void AppendEvent(RunEventRecord logEvent)
        {
            persistence.AppendEvent(logEvent);
        }

        public IReadOnlyCollection<RunEventRecord> GetEvents(string runId)
        {
            return persistence.GetEvents(runId);
        }

        public void RecordEnd(string runId)
        {
            persistence.CompleteRun(runId, "completed", DateTime.UtcNow);
        }

        public DungeonRunRecord RecordStart(string instanceId, string dungeonId, string partyId)
        {
            return persistence.BeginRun(dungeonId, partyId, DateTime.UtcNow);
        }
    }

    public class InMemoryLoginTokenRepository : ILoginTokenRepository
    {
        private readonly ConcurrentDictionary<string, (string PlayerId, DateTimeOffset ExpiresAt)> tokens = new();

        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(15);

        public string IssueToken(string playerId, TimeSpan? ttl = null)
        {
            var token = Guid.NewGuid().ToString("N");
            var expiresAt = DateTimeOffset.UtcNow.Add(ttl ?? DefaultTtl);
            tokens[token] = (playerId, expiresAt);
            return token;
        }

        public bool ValidateToken(string token, out string playerId)
        {
            if (tokens.TryGetValue(token, out var tuple) && tuple.ExpiresAt > DateTimeOffset.UtcNow)
            {
                playerId = tuple.PlayerId;
                return true;
            }

            playerId = string.Empty;
            return false;
        }

        public void RevokeToken(string token)
        {
            tokens.TryRemove(token, out _);
        }
    }

    public class InMemorySessionRepository : ISessionRepository
    {
        private readonly ConcurrentDictionary<string, Persistence.SessionStorageRecord> sessions = new();

        public void PersistSession(Persistence.SessionStorageRecord session)
        {
            sessions[session.SessionId] = session;
        }

        public void RemoveSession(string sessionId)
        {
            sessions.TryRemove(sessionId, out _);
        }

        public IReadOnlyCollection<Persistence.SessionStorageRecord> LoadActiveSessions()
        {
            return sessions.Values.ToArray();
        }
    }

    public class InMemoryDungeonRunRepository : IDungeonRunRepository
    {
        private readonly ConcurrentDictionary<string, DungeonRunRecord> runs = new();
        private readonly ConcurrentDictionary<string, List<RunEventRecord>> events = new();
        private long nextEventId;

        public DungeonRunRecord RecordStart(string instanceId, string dungeonId, string partyId)
        {
            var runId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
            var record = new DungeonRunRecord(runId, dungeonId, partyId, "in_progress", DateTime.UtcNow, null);
            runs[runId] = record;
            return record;
        }

        public void RecordEnd(string runId)
        {
            if (runs.TryGetValue(runId, out var record))
            {
                runs[runId] = record with { Status = "completed", EndedAt = DateTime.UtcNow };
            }
        }

        public void AppendEvent(RunEventRecord logEvent)
        {
            var eventId = System.Threading.Interlocked.Increment(ref nextEventId);
            var entry = logEvent with { EventId = eventId };
            var list = events.GetOrAdd(entry.RunId, _ => new List<RunEventRecord>());
            lock (list)
            {
                list.Add(entry);
            }
        }

        public IReadOnlyCollection<RunEventRecord> GetEvents(string runId)
        {
            if (!events.TryGetValue(runId, out var list))
            {
                return Array.Empty<RunEventRecord>();
            }

            lock (list)
            {
                return list.ToArray();
            }
        }

        public IReadOnlyCollection<DungeonRunRecord> Runs => runs.Values.ToArray();
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
