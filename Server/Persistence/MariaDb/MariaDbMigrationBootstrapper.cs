using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Adventure.Server.Persistence.MariaDb
{
    public class MariaDbMigrationBootstrapper : IMigrationBootstrapper
    {
        private readonly MariaDbConnectionFactory connectionFactory;
        private readonly string migrationDirectory;

        public MariaDbMigrationBootstrapper(MariaDbConnectionFactory connectionFactory, string migrationDirectory)
        {
            this.connectionFactory = connectionFactory;
            this.migrationDirectory = migrationDirectory;
        }

        public void Bootstrap()
        {
            if (!Directory.Exists(migrationDirectory))
            {
                throw new DirectoryNotFoundException($"Migration directory {migrationDirectory} was not found.");
            }

            var scripts = Directory
                .EnumerateFiles(migrationDirectory, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path)
                .ToArray();

            using var connection = connectionFactory.CreateConnection();
            connection.Open();

            EnsureMigrationTable(connection);
            var appliedMigrations = LoadAppliedMigrations(connection);
            var availableMigrations = scripts
                .Select(path => new MigrationFile(path, ComputeChecksum(path)))
                .ToList();

            ValidateDrift(appliedMigrations, availableMigrations);

            foreach (var migration in availableMigrations)
            {
                if (appliedMigrations.TryGetValue(migration.Id, out var checksum))
                {
                    if (!checksum.Equals(migration.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Migration drift detected for {migration.Id}.");
                    }

                    continue;
                }

                var sql = File.ReadAllText(migration.Path);
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.ExecuteNonQuery();

                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO schema_migrations (migration_id, checksum, applied_at) VALUES (@id, @checksum, @appliedAt);";
                var idParameter = insertCommand.CreateParameter();
                idParameter.ParameterName = "@id";
                idParameter.Value = migration.Id;
                insertCommand.Parameters.Add(idParameter);

                var checksumParameter = insertCommand.CreateParameter();
                checksumParameter.ParameterName = "@checksum";
                checksumParameter.Value = migration.Checksum;
                insertCommand.Parameters.Add(checksumParameter);

                var appliedParameter = insertCommand.CreateParameter();
                appliedParameter.ParameterName = "@appliedAt";
                appliedParameter.Value = DateTime.UtcNow;
                insertCommand.Parameters.Add(appliedParameter);

                insertCommand.ExecuteNonQuery();
            }
        }

        private static void EnsureMigrationTable(System.Data.IDbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS schema_migrations (
    migration_id VARCHAR(255) PRIMARY KEY,
    checksum CHAR(64) NOT NULL,
    applied_at DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            command.ExecuteNonQuery();
        }

        private static Dictionary<string, string> LoadAppliedMigrations(System.Data.IDbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT migration_id, checksum FROM schema_migrations;";
            using var reader = command.ExecuteReader();

            var applied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var checksum = reader.GetString(1);
                applied[id] = checksum;
            }

            return applied;
        }

        private static void ValidateDrift(Dictionary<string, string> applied, List<MigrationFile> available)
        {
            var availableIds = new HashSet<string>(available.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var appliedMigration in applied.Keys)
            {
                if (!availableIds.Contains(appliedMigration))
                {
                    throw new InvalidOperationException($"Migration drift detected: {appliedMigration} is applied but missing from disk.");
                }
            }
        }

        private static string ComputeChecksum(string path)
        {
            var contents = File.ReadAllText(path, Encoding.UTF8);
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(contents);
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private sealed record MigrationFile(string Path, string Checksum)
        {
            public string Id => System.IO.Path.GetFileName(Path);
        }
    }
}
