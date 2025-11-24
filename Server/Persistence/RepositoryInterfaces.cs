using System;
using System.Collections.Generic;
using Adventure.Server.Simulation;

namespace Adventure.Server.Persistence
{
    public record AccountRecord(string AccountId, string Email, string DisplayName, DateTime CreatedAt);

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

    public interface IDungeonRunRepository
    {
        DungeonRunRecord BeginRun(string dungeonId, string partyId, DateTime startedAtUtc);

        void CompleteRun(string runId, string status, DateTime? endedAtUtc = null);

        void AppendEvent(RunEventRecord logEvent);

        IReadOnlyCollection<RunEventRecord> GetEvents(string runId);
    }

    public interface IMigrationBootstrapper
    {
        void Bootstrap();
    }
}
