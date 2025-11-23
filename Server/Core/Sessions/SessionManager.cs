using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Adventure.Server.Core.Repositories;

namespace Adventure.Server.Core.Sessions
{
    public record SessionRecord(
        string SessionId,
        string PlayerId,
        DateTimeOffset ExpiresAt,
        string? ConnectionId,
        DateTimeOffset LastSeenUtc);

    /// <summary>
    /// Central session orchestrator. Issues login tokens, tracks active connections, and
    /// expires idle sessions based on configurable timeouts.
    /// </summary>
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, SessionRecord> sessionsById = new();
        private readonly ConcurrentDictionary<string, string> sessionIdByPlayer = new();
        private readonly ConcurrentDictionary<string, string> playerIdByConnection = new();
        private readonly ILoginTokenRepository loginTokens;
        private readonly ISessionRepository? sessionRepository;

        public TimeSpan SessionTtl { get; set; } = TimeSpan.FromMinutes(30);

        public SessionManager(ILoginTokenRepository loginTokens, ISessionRepository? sessionRepository = null)
        {
            this.loginTokens = loginTokens;
            this.sessionRepository = sessionRepository;
        }

        public string IssueLoginToken(string playerId)
        {
            return loginTokens.IssueToken(playerId);
        }

        public bool TryExchangeToken(string token, out SessionRecord session, string? connectionId = null)
        {
            if (!loginTokens.ValidateToken(token, out var playerId))
            {
                session = default!;
                return false;
            }

            session = IssueSession(playerId, connectionId);
            return true;
        }

        public SessionRecord IssueSession(string playerId, string? connectionId = null)
        {
            var sessionId = Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;
            var record = new SessionRecord(sessionId, playerId, now.Add(SessionTtl), connectionId, now);

            sessionsById[sessionId] = record;
            sessionIdByPlayer[playerId] = sessionId;
            sessionRepository?.PersistSession(sessionId, playerId);

            if (!string.IsNullOrWhiteSpace(connectionId))
            {
                playerIdByConnection[connectionId!] = playerId;
            }

            return record;
        }

        public bool TryGetSession(string sessionId, out SessionRecord record)
        {
            if (sessionsById.TryGetValue(sessionId, out record!))
            {
                if (record.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    return true;
                }

                RemoveSession(sessionId);
            }

            record = default!;
            return false;
        }

        public bool TryGetPlayerSession(string playerId, out SessionRecord record)
        {
            if (sessionIdByPlayer.TryGetValue(playerId, out var sessionId))
            {
                return TryGetSession(sessionId, out record);
            }

            record = default!;
            return false;
        }

        public bool AttachConnection(string sessionId, string connectionId)
        {
            if (!TryGetSession(sessionId, out var record))
            {
                return false;
            }

            var updated = record with { ConnectionId = connectionId, LastSeenUtc = DateTimeOffset.UtcNow };
            sessionsById[sessionId] = updated;
            playerIdByConnection[connectionId] = record.PlayerId;
            return true;
        }

        public bool TryGetPlayerIdForConnection(string connectionId, out string playerId)
        {
            return playerIdByConnection.TryGetValue(connectionId, out playerId!);
        }

        public void TouchSession(string sessionId)
        {
            if (sessionsById.TryGetValue(sessionId, out var record))
            {
                var updated = record with
                {
                    LastSeenUtc = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.Add(SessionTtl)
                };

                sessionsById[sessionId] = updated;
            }
        }

        public IEnumerable<SessionRecord> ExpireIdleSessions(DateTimeOffset? now = null)
        {
            now ??= DateTimeOffset.UtcNow;
            var expired = new List<SessionRecord>();

            foreach (var kvp in sessionsById.ToArray())
            {
                if (kvp.Value.ExpiresAt <= now)
                {
                    if (sessionsById.TryRemove(kvp.Key, out var removed))
                    {
                        expired.Add(removed);
                        sessionIdByPlayer.TryRemove(removed.PlayerId, out _);
                        if (!string.IsNullOrEmpty(removed.ConnectionId))
                        {
                            playerIdByConnection.TryRemove(removed.ConnectionId!, out _);
                        }
                        sessionRepository?.RemoveSession(removed.SessionId);
                    }
                }
            }

            return expired;
        }

        public void RemoveSession(string sessionId)
        {
            if (sessionsById.TryRemove(sessionId, out var removed))
            {
                sessionIdByPlayer.TryRemove(removed.PlayerId, out _);
                if (!string.IsNullOrEmpty(removed.ConnectionId))
                {
                    playerIdByConnection.TryRemove(removed.ConnectionId!, out _);
                }
                sessionRepository?.RemoveSession(sessionId);
            }
        }
    }
}
