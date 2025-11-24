using System;
using System.Collections.Generic;
using System.Linq;
using Adventure.Server.Persistence;

namespace Adventure.Server.Core.Sessions
{
    public class GameSessionState
    {
        public required GameSessionRecord Session { get; init; }
        public required IReadOnlyCollection<GameSessionMemberRecord> Members { get; init; }
    }

    /// <summary>
    /// Manages persistent dungeon/game sessions, preserving ownership and membership rules even across server restarts.
    /// </summary>
    public class GameSessionService
    {
        private readonly IGameSessionRepository repository;

        public GameSessionService(IGameSessionRepository repository)
        {
            this.repository = repository;
        }

        public GameSessionState CreateSession(string ownerAccountId, string? dungeonId = null)
        {
            var now = DateTime.UtcNow;
            var session = new GameSessionRecord(
                Guid.NewGuid().ToString("N"),
                ownerAccountId,
                "active",
                dungeonId,
                null,
                now,
                now);

            repository.Create(session);
            repository.AddMember(new GameSessionMemberRecord(session.SessionId, ownerAccountId, 0, now));

            return new GameSessionState
            {
                Session = session,
                Members = repository.GetMembers(session.SessionId)
            };
        }

        public GameSessionState? GetSession(string sessionId)
        {
            var session = repository.Get(sessionId);
            if (session == null)
            {
                return null;
            }

            return new GameSessionState
            {
                Session = session,
                Members = repository.GetMembers(session.SessionId)
            };
        }

        public GameSessionState? JoinSession(string sessionId, string accountId)
        {
            var session = repository.Get(sessionId);
            if (session == null || session.Status == "ended")
            {
                return null;
            }

            var members = repository.GetMembers(sessionId).ToList();
            if (members.Any(m => m.AccountId == accountId))
            {
                return new GameSessionState { Session = session, Members = members };
            }

            var nextOrder = members.Count == 0 ? 0 : members.Max(m => m.JoinOrder) + 1;
            repository.AddMember(new GameSessionMemberRecord(sessionId, accountId, nextOrder, DateTime.UtcNow));

            // players that rejoin must start fresh at the first room; callers enforce this when loading character state
            return new GameSessionState
            {
                Session = session,
                Members = repository.GetMembers(sessionId)
            };
        }

        public GameSessionState? LeaveSession(string sessionId, string accountId)
        {
            var session = repository.Get(sessionId);
            if (session == null)
            {
                return null;
            }

            var members = repository.GetMembers(sessionId).ToList();
            repository.RemoveMember(sessionId, accountId);
            members = repository.GetMembers(sessionId).ToList();

            if (members.Count == 0)
            {
                var ended = session with { Status = "ended", UpdatedAtUtc = DateTime.UtcNow };
                repository.Update(ended);
                return new GameSessionState { Session = ended, Members = Array.Empty<GameSessionMemberRecord>() };
            }

            if (session.OwnerAccountId == accountId)
            {
                var newOwner = members.OrderBy(m => m.JoinOrder).First();
                session = session with { OwnerAccountId = newOwner.AccountId, UpdatedAtUtc = DateTime.UtcNow };
                repository.Update(session);
            }

            return new GameSessionState { Session = session, Members = members };
        }

        public GameSessionState? SaveSession(string sessionId, string ownerAccountId, string stateJson)
        {
            var session = repository.Get(sessionId);
            if (session == null || session.OwnerAccountId != ownerAccountId)
            {
                return null;
            }

            var updated = session with
            {
                Status = "saved",
                SavedStateJson = stateJson,
                UpdatedAtUtc = DateTime.UtcNow
            };

            repository.Update(updated);

            // Only the owner remains associated with the saved session; others must rejoin later
            repository.ClearMembers(sessionId);
            repository.AddMember(new GameSessionMemberRecord(sessionId, ownerAccountId, 0, DateTime.UtcNow));

            return new GameSessionState
            {
                Session = updated,
                Members = repository.GetMembers(sessionId)
            };
        }

        public GameSessionState? ResumeSession(string sessionId, string ownerAccountId)
        {
            var session = repository.Get(sessionId);
            if (session == null || session.OwnerAccountId != ownerAccountId)
            {
                return null;
            }

            var updated = session with { Status = "active", UpdatedAtUtc = DateTime.UtcNow };
            repository.Update(updated);

            // Resuming clears empty slots; players must join again and will start at the first room
            repository.ClearMembers(sessionId);
            repository.AddMember(new GameSessionMemberRecord(sessionId, ownerAccountId, 0, DateTime.UtcNow));

            return new GameSessionState
            {
                Session = updated,
                Members = repository.GetMembers(sessionId)
            };
        }
    }
}
