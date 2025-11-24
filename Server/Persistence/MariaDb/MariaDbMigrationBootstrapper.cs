using System;
using System.IO;
using System.Linq;

namespace Adventure.Server.Persistence.MariaDb
{
    public class MariaDbMigrationBootstrapper : IMigrationBootstrapper
    {
        private readonly MariaDbConnectionFactory connectionFactory;
        private readonly string schemaDirectory;

        public MariaDbMigrationBootstrapper(MariaDbConnectionFactory connectionFactory, string schemaDirectory)
        {
            this.connectionFactory = connectionFactory;
            this.schemaDirectory = schemaDirectory;
        }

        public void Bootstrap()
        {
            if (!Directory.Exists(schemaDirectory))
            {
                throw new DirectoryNotFoundException($"Schema directory {schemaDirectory} was not found.");
            }

            var scripts = Directory
                .EnumerateFiles(schemaDirectory, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path)
                .ToArray();

            using var connection = connectionFactory.CreateConnection();
            connection.Open();

            foreach (var script in scripts)
            {
                var sql = File.ReadAllText(script);
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
