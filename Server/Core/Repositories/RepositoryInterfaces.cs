using System.Collections.Generic;
using Adventure.Server.Core.Lobby;

namespace Adventure.Server.Core.Repositories
{
    public record PlayerProfile(string PlayerId, string DisplayName, int Level);

    public interface IPlayerProfileRepository
    {
        PlayerProfile GetProfile(string playerId);
    }

    public interface IPartyRepository
    {
        PartyState? GetParty(string partyId);
        void SaveParty(PartyState party);
        void DeleteParty(string partyId);
    }

    public interface IChatHistoryRepository
    {
        void Append(ChatLogEntry entry);
        IReadOnlyCollection<ChatLogEntry> Recent(string channelId, int limit = 100);
    }

    public interface ISessionRepository
    {
        void PersistSession(string sessionId, string playerId);
        void RemoveSession(string sessionId);
    }

    public interface ILoginTokenRepository
    {
        string IssueToken(string playerId);
        bool ValidateToken(string token, out string playerId);
    }

    public interface IDungeonRunRepository
    {
        void RecordStart(string instanceId, string dungeonId, string partyId);
        void RecordEnd(string instanceId);
    }

    public interface IDungeonSimulationFactory
    {
        IDungeonSimulation Create(string dungeonId);
    }

    public interface IDungeonSimulation
    {
        System.Threading.Tasks.Task RunAsync(string instanceId, string dungeonId, IEnumerable<LobbyMemberPresence> members, System.Threading.CancellationToken cancellationToken);
        System.Threading.Tasks.Task HandlePlayerEventAsync(string instanceId, string playerId, object evt, System.Threading.CancellationToken cancellationToken);
    }
}
