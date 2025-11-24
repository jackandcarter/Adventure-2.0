using System;
using System.Collections.Generic;
using System.Data;
using Adventure.Server.Persistence;
using Adventure.Server.Simulation;

namespace Adventure.Server.Persistence.MariaDb
{
    public abstract class MariaDbRepository
    {
        private readonly MariaDbConnectionFactory connectionFactory;

        protected MariaDbRepository(MariaDbConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        protected IDbConnection OpenConnection()
        {
            var connection = connectionFactory.CreateConnection();
            connection.Open();
            return connection;
        }

        protected static void AddParameter(IDbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    public class MariaDbAccountRepository : MariaDbRepository, IAccountRepository
    {
        public MariaDbAccountRepository(MariaDbConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public string Create(AccountRecord account)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO accounts (account_id, email, display_name, created_at)
VALUES (@id, @email, @displayName, @createdAt);";

            AddParameter(command, "@id", account.AccountId);
            AddParameter(command, "@email", account.Email);
            AddParameter(command, "@displayName", account.DisplayName);
            AddParameter(command, "@createdAt", account.CreatedAt);

            command.ExecuteNonQuery();
            return account.AccountId;
        }

        public AccountRecord? GetByEmail(string email)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT account_id, email, display_name, created_at FROM accounts WHERE email = @email LIMIT 1;";
            AddParameter(command, "@email", email);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return MapAccount(reader);
        }

        public AccountRecord? GetById(string accountId)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT account_id, email, display_name, created_at FROM accounts WHERE account_id = @id LIMIT 1;";
            AddParameter(command, "@id", accountId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return MapAccount(reader);
        }

        private static AccountRecord MapAccount(IDataRecord reader)
        {
            return new AccountRecord(
                reader.GetString("account_id"),
                reader.GetString("email"),
                reader.GetString("display_name"),
                reader.GetDateTime("created_at"));
        }
    }

    public class MariaDbCharacterRepository : MariaDbRepository, ICharacterRepository
    {
        public MariaDbCharacterRepository(MariaDbConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public CharacterRecord? GetById(string characterId)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM characters WHERE character_id = @id LIMIT 1;";
            AddParameter(command, "@id", characterId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return MapCharacter(reader);
        }

        public IReadOnlyCollection<CharacterRecord> GetByAccount(string accountId)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM characters WHERE account_id = @accountId;";
            AddParameter(command, "@accountId", accountId);

            using var reader = command.ExecuteReader();
            var results = new List<CharacterRecord>();
            while (reader.Read())
            {
                results.Add(MapCharacter(reader));
            }

            return results;
        }

        public void Save(CharacterRecord character)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO characters (
    character_id, account_id, name, class, level, max_health, max_mana, attack_power,
    magic_power, speed, defense, magic_resist, tenacity, precision, awareness,
    crit_rating, evade_rating, trance_generation_bonus, trance_spend_efficiency,
    cosmetic_primary_color, cosmetic_secondary_color, cosmetic_outfit_id, created_at, updated_at)
VALUES (
    @id, @accountId, @name, @class, @level, @maxHealth, @maxMana, @attackPower,
    @magicPower, @speed, @defense, @magicResist, @tenacity, @precision, @awareness,
    @critRating, @evadeRating, @tranceGenerationBonus, @tranceSpendEfficiency,
    @cosmeticPrimary, @cosmeticSecondary, @cosmeticOutfit, @createdAt, @updatedAt)
ON DUPLICATE KEY UPDATE
    name = VALUES(name),
    class = VALUES(class),
    level = VALUES(level),
    max_health = VALUES(max_health),
    max_mana = VALUES(max_mana),
    attack_power = VALUES(attack_power),
    magic_power = VALUES(magic_power),
    speed = VALUES(speed),
    defense = VALUES(defense),
    magic_resist = VALUES(magic_resist),
    tenacity = VALUES(tenacity),
    precision = VALUES(precision),
    awareness = VALUES(awareness),
    crit_rating = VALUES(crit_rating),
    evade_rating = VALUES(evade_rating),
    trance_generation_bonus = VALUES(trance_generation_bonus),
    trance_spend_efficiency = VALUES(trance_spend_efficiency),
    cosmetic_primary_color = VALUES(cosmetic_primary_color),
    cosmetic_secondary_color = VALUES(cosmetic_secondary_color),
    cosmetic_outfit_id = VALUES(cosmetic_outfit_id),
    updated_at = VALUES(updated_at);";

            AddCharacterParameters(command, character);
            command.ExecuteNonQuery();
        }

        public void UpdateCosmetics(string characterId, CharacterCosmetics cosmetics)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE characters
SET cosmetic_primary_color = @primary,
    cosmetic_secondary_color = @secondary,
    cosmetic_outfit_id = @outfit,
    updated_at = @updatedAt
WHERE character_id = @id;";

            AddParameter(command, "@primary", cosmetics.PrimaryColor);
            AddParameter(command, "@secondary", cosmetics.SecondaryColor);
            AddParameter(command, "@outfit", cosmetics.OutfitId);
            AddParameter(command, "@updatedAt", DateTime.UtcNow);
            AddParameter(command, "@id", characterId);
            command.ExecuteNonQuery();
        }

        public void UpdateStats(string characterId, StatSnapshot stats)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE characters
SET level = @level,
    max_health = @maxHealth,
    max_mana = @maxMana,
    attack_power = @attackPower,
    magic_power = @magicPower,
    speed = @speed,
    defense = @defense,
    magic_resist = @magicResist,
    tenacity = @tenacity,
    precision = @precision,
    awareness = @awareness,
    crit_rating = @critRating,
    evade_rating = @evadeRating,
    trance_generation_bonus = @tranceGenerationBonus,
    trance_spend_efficiency = @tranceSpendEfficiency,
    updated_at = @updatedAt
WHERE character_id = @id;";

            AddParameter(command, "@level", stats.Level);
            AddParameter(command, "@maxHealth", stats.MaxHealth);
            AddParameter(command, "@maxMana", stats.MaxMana);
            AddParameter(command, "@attackPower", stats.AttackPower);
            AddParameter(command, "@magicPower", stats.MagicPower);
            AddParameter(command, "@speed", stats.Speed);
            AddParameter(command, "@defense", stats.Defense);
            AddParameter(command, "@magicResist", stats.MagicResist);
            AddParameter(command, "@tenacity", stats.Tenacity);
            AddParameter(command, "@precision", stats.Precision);
            AddParameter(command, "@awareness", stats.Awareness);
            AddParameter(command, "@critRating", stats.CritRating);
            AddParameter(command, "@evadeRating", stats.EvadeRating);
            AddParameter(command, "@tranceGenerationBonus", stats.TranceGenerationBonus);
            AddParameter(command, "@tranceSpendEfficiency", stats.TranceSpendEfficiency);
            AddParameter(command, "@updatedAt", DateTime.UtcNow);
            AddParameter(command, "@id", characterId);
            command.ExecuteNonQuery();
        }

        private static CharacterRecord MapCharacter(IDataRecord reader)
        {
            var stats = new CharacterStats(
                reader.GetInt32("level"),
                reader.GetFloat("max_health"),
                reader.GetFloat("max_mana"),
                reader.GetFloat("attack_power"),
                reader.GetFloat("magic_power"),
                reader.GetFloat("speed"),
                reader.GetFloat("defense"),
                reader.GetFloat("magic_resist"),
                reader.GetFloat("tenacity"),
                reader.GetFloat("precision"),
                reader.GetFloat("awareness"),
                reader.GetFloat("crit_rating"),
                reader.GetFloat("evade_rating"),
                reader.GetFloat("trance_generation_bonus"),
                reader.GetFloat("trance_spend_efficiency"));

            var cosmetics = new CharacterCosmetics(
                reader.GetString("cosmetic_primary_color"),
                reader.GetString("cosmetic_secondary_color"),
                reader.GetString("cosmetic_outfit_id"));

            return new CharacterRecord(
                reader.GetString("character_id"),
                reader.GetString("account_id"),
                reader.GetString("name"),
                reader.GetString("class"),
                stats,
                cosmetics,
                reader.GetDateTime("created_at"),
                reader.GetDateTime("updated_at"));
        }

        private static void AddCharacterParameters(IDbCommand command, CharacterRecord character)
        {
            AddParameter(command, "@id", character.CharacterId);
            AddParameter(command, "@accountId", character.AccountId);
            AddParameter(command, "@name", character.Name);
            AddParameter(command, "@class", character.Class);
            AddParameter(command, "@level", character.Stats.Level);
            AddParameter(command, "@maxHealth", character.Stats.MaxHealth);
            AddParameter(command, "@maxMana", character.Stats.MaxMana);
            AddParameter(command, "@attackPower", character.Stats.AttackPower);
            AddParameter(command, "@magicPower", character.Stats.MagicPower);
            AddParameter(command, "@speed", character.Stats.Speed);
            AddParameter(command, "@defense", character.Stats.Defense);
            AddParameter(command, "@magicResist", character.Stats.MagicResist);
            AddParameter(command, "@tenacity", character.Stats.Tenacity);
            AddParameter(command, "@precision", character.Stats.Precision);
            AddParameter(command, "@awareness", character.Stats.Awareness);
            AddParameter(command, "@critRating", character.Stats.CritRating);
            AddParameter(command, "@evadeRating", character.Stats.EvadeRating);
            AddParameter(command, "@tranceGenerationBonus", character.Stats.TranceGenerationBonus);
            AddParameter(command, "@tranceSpendEfficiency", character.Stats.TranceSpendEfficiency);
            AddParameter(command, "@cosmeticPrimary", character.Cosmetics.PrimaryColor);
            AddParameter(command, "@cosmeticSecondary", character.Cosmetics.SecondaryColor);
            AddParameter(command, "@cosmeticOutfit", character.Cosmetics.OutfitId);
            AddParameter(command, "@createdAt", character.CreatedAt);
            AddParameter(command, "@updatedAt", character.UpdatedAt);
        }
    }

    public class MariaDbInventoryRepository : MariaDbRepository, IInventoryRepository
    {
        public MariaDbInventoryRepository(MariaDbConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public void ClearForCharacter(string characterId)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM inventory WHERE character_id = @characterId;";
            AddParameter(command, "@characterId", characterId);
            command.ExecuteNonQuery();
        }

        public IReadOnlyCollection<InventoryItemRecord> GetForCharacter(string characterId)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM inventory WHERE character_id = @characterId;";
            AddParameter(command, "@characterId", characterId);

            using var reader = command.ExecuteReader();
            var items = new List<InventoryItemRecord>();
            while (reader.Read())
            {
                items.Add(MapInventoryItem(reader));
            }

            return items;
        }

        public void Remove(string inventoryItemId)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM inventory WHERE inventory_item_id = @id;";
            AddParameter(command, "@id", inventoryItemId);
            command.ExecuteNonQuery();
        }

        public void Upsert(InventoryItemRecord item)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO inventory (
    inventory_item_id, character_id, slot, item_definition_id, quantity, acquired_at)
VALUES (@id, @characterId, @slot, @definitionId, @quantity, @acquiredAt)
ON DUPLICATE KEY UPDATE
    slot = VALUES(slot),
    item_definition_id = VALUES(item_definition_id),
    quantity = VALUES(quantity),
    acquired_at = VALUES(acquired_at);";

            AddParameter(command, "@id", item.InventoryItemId);
            AddParameter(command, "@characterId", item.CharacterId);
            AddParameter(command, "@slot", item.Slot);
            AddParameter(command, "@definitionId", item.ItemDefinitionId);
            AddParameter(command, "@quantity", item.Quantity);
            AddParameter(command, "@acquiredAt", item.AcquiredAt);
            command.ExecuteNonQuery();
        }

        private static InventoryItemRecord MapInventoryItem(IDataRecord reader)
        {
            return new InventoryItemRecord(
                reader.GetString("inventory_item_id"),
                reader.GetString("character_id"),
                reader.GetString("slot"),
                reader.GetString("item_definition_id"),
                reader.GetInt32("quantity"),
                reader.GetDateTime("acquired_at"));
        }
    }

    public class MariaDbUnlockRepository : MariaDbRepository, IUnlockRepository
    {
        public MariaDbUnlockRepository(MariaDbConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public void Add(UnlockRecord unlock)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO unlocks (unlock_id, account_id, unlock_key, unlocked_at)
VALUES (@id, @accountId, @unlockKey, @unlockedAt)
ON DUPLICATE KEY UPDATE unlocked_at = VALUES(unlocked_at);";

            AddParameter(command, "@id", unlock.UnlockId);
            AddParameter(command, "@accountId", unlock.AccountId);
            AddParameter(command, "@unlockKey", unlock.UnlockKey);
            AddParameter(command, "@unlockedAt", unlock.UnlockedAt);
            command.ExecuteNonQuery();
        }

        public IReadOnlyCollection<UnlockRecord> GetForAccount(string accountId)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM unlocks WHERE account_id = @accountId;";
            AddParameter(command, "@accountId", accountId);

            using var reader = command.ExecuteReader();
            var unlocks = new List<UnlockRecord>();
            while (reader.Read())
            {
                unlocks.Add(new UnlockRecord(
                    reader.GetString("unlock_id"),
                    reader.GetString("account_id"),
                    reader.GetString("unlock_key"),
                    reader.GetDateTime("unlocked_at")));
            }

            return unlocks;
        }
    }

    public class MariaDbDungeonRunRepository : MariaDbRepository, IDungeonRunRepository
    {
        public MariaDbDungeonRunRepository(MariaDbConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public void AppendEvent(RunEventRecord logEvent)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO run_events (run_id, event_type, payload_json, occurred_at)
VALUES (@runId, @eventType, @payload, @occurredAt);";

            AddParameter(command, "@runId", logEvent.RunId);
            AddParameter(command, "@eventType", logEvent.EventType);
            AddParameter(command, "@payload", logEvent.PayloadJson);
            AddParameter(command, "@occurredAt", logEvent.OccurredAt);
            command.ExecuteNonQuery();
        }

        public DungeonRunRecord BeginRun(string dungeonId, string partyId, DateTime startedAtUtc)
        {
            var runId = Guid.NewGuid().ToString("N");
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO dungeon_runs (run_id, dungeon_id, party_id, status, started_at)
VALUES (@id, @dungeonId, @partyId, @status, @startedAt);";

            AddParameter(command, "@id", runId);
            AddParameter(command, "@dungeonId", dungeonId);
            AddParameter(command, "@partyId", partyId);
            AddParameter(command, "@status", "in_progress");
            AddParameter(command, "@startedAt", startedAtUtc);
            command.ExecuteNonQuery();

            return new DungeonRunRecord(runId, dungeonId, partyId, "in_progress", startedAtUtc, null);
        }

        public void CompleteRun(string runId, string status, DateTime? endedAtUtc = null)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE dungeon_runs
SET status = @status,
    ended_at = COALESCE(@endedAt, ended_at)
WHERE run_id = @runId;";

            AddParameter(command, "@status", status);
            AddParameter(command, "@endedAt", endedAtUtc ?? DateTime.UtcNow);
            AddParameter(command, "@runId", runId);
            command.ExecuteNonQuery();
        }

        public IReadOnlyCollection<RunEventRecord> GetEvents(string runId)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT event_id, run_id, event_type, payload_json, occurred_at
FROM run_events WHERE run_id = @runId ORDER BY occurred_at;";
            AddParameter(command, "@runId", runId);

            using var reader = command.ExecuteReader();
            var events = new List<RunEventRecord>();
            while (reader.Read())
            {
                events.Add(new RunEventRecord(
                    reader.GetInt64("event_id"),
                    reader.GetString("run_id"),
                    reader.GetString("event_type"),
                    reader.GetString("payload_json"),
                    reader.GetDateTime("occurred_at")));
            }

            return events;
        }
    }

    internal static class DataRecordExtensions
    {
        public static string GetString(this IDataRecord record, string column)
        {
            return record[column].ToString() ?? string.Empty;
        }

        public static DateTime GetDateTime(this IDataRecord record, string column)
        {
            return Convert.ToDateTime(record[column]);
        }

        public static float GetFloat(this IDataRecord record, string column)
        {
            return Convert.ToSingle(record[column]);
        }

        public static int GetInt32(this IDataRecord record, string column)
        {
            return Convert.ToInt32(record[column]);
        }

        public static long GetInt64(this IDataRecord record, string column)
        {
            return Convert.ToInt64(record[column]);
        }
    }
}
