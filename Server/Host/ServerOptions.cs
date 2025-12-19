using System;
using System.Text;

namespace Adventure.Server.Host
{
    public class ServerOptions
    {
        public string ListenAddress { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 5000;
        public int TickRateHz { get; set; } = 20;
        public string AuthSecret { get; set; } = string.Empty;
        public string LogLevel { get; set; } = "Information";
    }

    public class DatabaseOptions
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 3306;
        public string Name { get; set; } = "adventure";
        public string User { get; set; } = "root";
        public string Password { get; set; } = "pass";
        public string? ConnectionString { get; set; }

        public string BuildConnectionString()
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                return ConnectionString!;
            }

            var builder = new StringBuilder();
            builder.Append($"Server={Host};");
            builder.Append($"Port={Port};");
            builder.Append($"Database={Name};");
            builder.Append($"Uid={User};");
            builder.Append($"Pwd={Password};");
            return builder.ToString();
        }
    }

    public class RuntimeState
    {
        public bool Ready { get; private set; }
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public string? NotReadyReason { get; private set; }

        public void MarkReady()
        {
            Ready = true;
            NotReadyReason = null;
        }

        public void MarkNotReady(string reason)
        {
            Ready = false;
            NotReadyReason = reason;
        }
    }
}
