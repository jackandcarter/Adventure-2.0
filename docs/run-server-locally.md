# Run the server locally

## Prerequisites

- .NET SDK 8.0
- MariaDB 10.6+ (or compatible MySQL server)

## 1) Configure environment variables

The server reads configuration from `appsettings.json` and the `ADVENTURE_` environment variable prefix. Common settings:

| Setting | Example | Description |
| --- | --- | --- |
| `ADVENTURE_Database__Host` | `localhost` | MariaDB host |
| `ADVENTURE_Database__Port` | `3306` | MariaDB port |
| `ADVENTURE_Database__Name` | `adventure` | Database name |
| `ADVENTURE_Database__User` | `root` | Database user |
| `ADVENTURE_Database__Password` | `pass` | Database password |
| `ADVENTURE_Database__ConnectionString` | `Server=...;Port=...;Database=...;Uid=...;Pwd=...;` | Optional full connection string (overrides the individual DB values) |
| `ADVENTURE_Server__ListenAddress` | `0.0.0.0` | Bind address |
| `ADVENTURE_Server__Port` | `5000` | Bind port |
| `ADVENTURE_Server__TickRateHz` | `20` | Simulation tick rate |
| `ADVENTURE_Server__AuthSecret` | `change-me` | Shared secret for auth/ops workflows |
| `ADVENTURE_Server__LogLevel` | `Information` | Log level (`Trace`, `Debug`, `Information`, `Warning`, `Error`) |

## 2) Run migrations

Run the migration command once to apply schema changes and validate drift:

```bash
dotnet run --project Server/Host -- migrate
```

The command will fail if any applied migration no longer matches the SQL on disk.

## 3) Launch the server

```bash
dotnet run --project Server/Host
```

The server runs Swagger at `http://localhost:5000/swagger` by default and exposes:

- WebSocket endpoint: `ws://localhost:5000/ws`
- Health check: `http://localhost:5000/health`
- Readiness check: `http://localhost:5000/ready`

### Seed data

On startup, the server checks for empty reference tables (`abilities`, `room_templates`) and inserts a small default set to support smoke tests.

## Troubleshooting

### Migration drift errors

If the server reports drift, compare the SQL file on disk in `db/migrations` with the entry in the `schema_migrations` table. Fix by restoring the expected migration file or resetting the database if it is safe to do so.

### Connection refused / access denied

- Verify MariaDB is running and accessible on the configured host/port.
- Confirm database credentials in the environment variables.
- Ensure the database exists (e.g., `CREATE DATABASE adventure;`).

### Readiness probe stays unavailable

- The server must connect to MariaDB to report ready. Check database connectivity and logs for any migration failures.
