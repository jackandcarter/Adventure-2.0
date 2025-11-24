using System.Data;
using MySql.Data.MySqlClient;

namespace Adventure.Server.Persistence.MariaDb
{
    public class MariaDbConnectionFactory
    {
        private readonly string connectionString;

        public MariaDbConnectionFactory(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IDbConnection CreateConnection()
        {
            return new MySqlConnection(connectionString);
        }
    }
}
