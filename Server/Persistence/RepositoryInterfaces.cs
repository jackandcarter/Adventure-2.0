using System;
using System.Collections.Generic;
using Adventure.Server.Simulation;

namespace Adventure.Server.Persistence
{
    public record AccountRecord(
        string AccountId,
        string Email,
        string DisplayName,
        string PasswordHash,
        bool EmailVerified,
        DateTime CreatedAt);

    public record CharacterCosmetics(string PrimaryColor, string SecondaryColor, string OutfitId);

    public record CharacterStats(
        int Level,
        float MaxHealth,
        float MaxMana,
        float AttackPower,
        float MagicPower,
        float Speed,
        float Defense,
        float MagicResist,
        float Tenacity,
        float Precision,
        float Awareness,
        float CritRating,
        float EvadeRating,
        float TranceGenerationBonus,
        float TranceSpendEfficiency);

    public record CharacterRecord(
        string CharacterId,
        string AccountId,
        string Name,
        string Class,
        CharacterStats Stats,
        CharacterCosmetics Cosmetics,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public record InventoryItemRecord(
        string InventoryItemId,
        string CharacterId,
        string Slot,
        string ItemDefinitionId,
        int Quantity,
        DateTime AcquiredAt);

    public record UnlockRecord(string UnlockId, string AccountId, string UnlockKey, DateTime UnlockedAt);

    public record DungeonRunRecord(
        string RunId,
        string DungeonId,
        string PartyId,
        string Status,
        DateTime StartedAt,
        DateTime? EndedAt);

    public record RunEventRecord(long EventId, string RunId, string EventType, string PayloadJson, DateTime OccurredAt);

    public interface IAccountRepository
    {
        AccountRecord? GetById(string accountId);

        AccountRecord? GetByEmail(string email);

        string Create(AccountRecord account);

        void SetPasswordHash(string accountId, string passwordHash);

        void MarkEmailVerified(string accountId);
    }

    public interface ICharacterRepository
    {
        CharacterRecord? GetById(string characterId);

        IReadOnlyCollection<CharacterRecord> GetByAccount(string accountId);

        void Save(CharacterRecord character);

        void UpdateStats(string characterId, StatSnapshot stats);

        void UpdateCosmetics(string characterId, CharacterCosmetics cosmetics);
    }

    public interface IInventoryRepository
    {
        IReadOnlyCollection<InventoryItemRecord> GetForCharacter(string characterId);

        void Upsert(InventoryItemRecord item);

        void Remove(string inventoryItemId);

        void ClearForCharacter(string characterId);
    }

    public interface IUnlockRepository
    {
        IReadOnlyCollection<UnlockRecord> GetForAccount(string accountId);

        void Add(UnlockRecord unlock);
    }

    public interface ILoginTokenRepository
    {
        string IssueToken(string playerId, TimeSpan? ttl = null);
        bool ValidateToken(string token, out string playerId);
        void RevokeToken(string token);
    }

    public interface ISessionRepository
    {
        void PersistSession(SessionStorageRecord session);
        void RemoveSession(string sessionId);
        IReadOnlyCollection<SessionStorageRecord> LoadActiveSessions();
    }

    public record EmailVerificationRecord(string VerificationId, string AccountId, string Token, DateTime ExpiresAtUtc, DateTime? VerifiedAtUtc);

    public interface IEmailVerificationRepository
    {
        void Create(EmailVerificationRecord record);
        EmailVerificationRecord? GetByToken(string token);
        void MarkVerified(string verificationId, DateTime verifiedAtUtc);
    }

    public record SessionStorageRecord(
        string SessionId,
        string PlayerId,
        DateTimeOffset ExpiresAt,
        string? ConnectionId,
        DateTimeOffset LastSeenUtc);

    public interface IDungeonRunRepository
    {
        DungeonRunRecord BeginRun(string dungeonId, string partyId, DateTime startedAtUtc);

        void CompleteRun(string runId, string status, DateTime? endedAtUtc = null);

        void AppendEvent(RunEventRecord logEvent);

        IReadOnlyCollection<RunEventRecord> GetEvents(string runId);
    }

    public record GameSessionRecord(
        string SessionId,
        string OwnerAccountId,
        string Status,
        string? DungeonId,
        string? SavedStateJson,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    public record GameSessionMemberRecord(string SessionId, string AccountId, int JoinOrder, DateTime JoinedAtUtc);

    public interface IGameSessionRepository
    {
        GameSessionRecord Create(GameSessionRecord record);
        GameSessionRecord? Get(string sessionId);
        void Update(GameSessionRecord record);
        IReadOnlyCollection<GameSessionMemberRecord> GetMembers(string sessionId);
        void AddMember(GameSessionMemberRecord member);
        void RemoveMember(string sessionId, string accountId);
        void ClearMembers(string sessionId);
    }

    public interface IMigrationBootstrapper
    {
        void Bootstrap();
    }

    public interface IReferenceDataSeeder
    {
        void SeedReferenceData();
    }
}
