using System;
using System.Data;

namespace Adventure.Server.Persistence.MariaDb
{
    public class MariaDbReferenceDataSeeder : IReferenceDataSeeder
    {
        private readonly MariaDbConnectionFactory connectionFactory;

        public MariaDbReferenceDataSeeder(MariaDbConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public void SeedReferenceData()
        {
            using var connection = connectionFactory.CreateConnection();
            connection.Open();

            if (IsTableEmpty(connection, "abilities"))
            {
                InsertAbility(connection, "basic-attack", "Basic Attack", "A quick strike used for smoke tests.");
                InsertAbility(connection, "arcane-bolt", "Arcane Bolt", "A simple ranged blast.");
            }

            if (IsTableEmpty(connection, "room_templates"))
            {
                InsertRoomTemplate(connection, "starter-room", "Starter Room", "safe");
                InsertRoomTemplate(connection, "enemy-room", "Enemy Room", "enemy");
            }
        }

        private static bool IsTableEmpty(IDbConnection connection, string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            var count = Convert.ToInt32(command.ExecuteScalar());
            return count == 0;
        }

        private static void InsertAbility(IDbConnection connection, string id, string name, string description)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO abilities (ability_id, display_name, description, created_at)
VALUES (@id, @name, @description, @createdAt);";
            AddParameter(command, "@id", id);
            AddParameter(command, "@name", name);
            AddParameter(command, "@description", description);
            AddParameter(command, "@createdAt", DateTime.UtcNow);
            command.ExecuteNonQuery();
        }

        private static void InsertRoomTemplate(IDbConnection connection, string id, string name, string type)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO room_templates (template_id, display_name, template_type, created_at)
VALUES (@id, @name, @type, @createdAt);";
            AddParameter(command, "@id", id);
            AddParameter(command, "@name", name);
            AddParameter(command, "@type", type);
            AddParameter(command, "@createdAt", DateTime.UtcNow);
            command.ExecuteNonQuery();
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }
}
