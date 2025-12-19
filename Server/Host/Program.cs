using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Adventure.Server.Core.Configuration;
using Adventure.Server.Core.Repositories;
using Adventure.Server.Core.Sessions;
using Adventure.Server.Persistence;
using Adventure.Server.Persistence.MariaDb;
using Adventure.Server.Network;
using Adventure.Server.Simulation;
using Adventure.Server.Host;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "ADVENTURE_");

var serverOptions = builder.Configuration.GetSection("Server").Get<ServerOptions>() ?? new ServerOptions();
var databaseOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
var smtpOptions = builder.Configuration.GetSection("Mail").Get<SmtpOptions>() ?? new SmtpOptions();

databaseOptions.ConnectionString ??= builder.Configuration.GetConnectionString("MariaDb")
    ?? builder.Configuration["Database:ConnectionString"];

var logLevel = TryParseLogLevel(serverOptions.LogLevel);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(logLevel);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton(serverOptions);
builder.Services.AddSingleton(databaseOptions);
builder.Services.AddSingleton(smtpOptions);
builder.Services.AddSingleton(new RuntimeState());

var migrationDirectory = ResolveDirectory(builder.Environment.ContentRootPath, Path.Combine("db", "migrations"));
var connectionString = databaseOptions.BuildConnectionString();
ServerServiceConfigurator.ConfigureServices(builder.Services, connectionString, migrationDirectory);

builder.Services.AddSingleton(new SessionRegistry());
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton(new WebSocketConnectionRegistry());
builder.Services.AddSingleton(new WebSocketListenerOptions());
builder.Services.AddSingleton<WebSocketListener>();
builder.Services.AddSingleton(_ => new SimulationLoop(serverOptions.TickRateHz));

var simulationDataDirectory = ResolveDirectory(builder.Environment.ContentRootPath, Path.Combine("Server", "Simulation", "Data"));
builder.Services.AddSingleton(provider =>
{
    var logger = provider.GetRequiredService<ILogger<SimulationDataLoader>>();
    return SimulationDataLoader.LoadFromDirectory(simulationDataDirectory, logger);
});
builder.Services.AddSingleton(provider => provider.GetRequiredService<SimulationCatalogs>().Abilities);
builder.Services.AddSingleton(provider => provider.GetRequiredService<SimulationCatalogs>().EnemyArchetypes);
builder.Services.AddSingleton(provider => provider.GetRequiredService<SimulationCatalogs>().LootTables);

builder.WebHost.UseUrls($"http://{serverOptions.ListenAddress}:{serverOptions.Port}");

var app = builder.Build();

if (args.Length > 0 && args[0].Equals("migrate", StringComparison.OrdinalIgnoreCase))
{
    ApplyMigrationsAndSeed(app.Services);
    return;
}

ApplyMigrationsAndSeed(app.Services);

app.Lifetime.ApplicationStopping.Register(() =>
{
    var state = app.Services.GetRequiredService<RuntimeState>();
    state.MarkNotReady("Shutting down");
    app.Logger.LogInformation("Shutdown signal received. Server is stopping.");
});

if (string.IsNullOrWhiteSpace(serverOptions.AuthSecret))
{
    app.Logger.LogWarning("Server auth secret is not configured. Set ADVENTURE_Server__AuthSecret for production.");
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

app.Map("/ws", async (HttpContext context, WebSocketListener listener) =>
{
    await listener.AcceptAsync(context);
});

app.MapPost("/api/register", async (RegisterRequest request, IAccountRepository accountRepository, IEmailVerificationRepository verificationRepository, PasswordHasher hasher, SmtpOptions mailOptions) =>
{
    var existing = accountRepository.GetByEmail(request.Email);
    if (existing != null)
    {
        return Results.Conflict(new { message = "Email already registered." });
    }

    var accountId = Guid.NewGuid().ToString("N");
    var hash = hasher.HashPassword(request.Password);
    var account = new AccountRecord(accountId, request.Email, request.DisplayName, hash, false, DateTime.UtcNow);
    accountRepository.Create(account);

    var verificationToken = Guid.NewGuid().ToString("N");
    var verification = new EmailVerificationRecord(Guid.NewGuid().ToString("N"), accountId, verificationToken, DateTime.UtcNow.AddHours(4), null);
    verificationRepository.Create(verification);

    await SendVerificationEmailAsync(mailOptions, request.Email, verificationToken);

    return Results.Ok(new { accountId, verificationSent = true });
});

app.MapGet("/verify", (string token, IEmailVerificationRepository verificationRepository, IAccountRepository accountRepository) =>
{
    var record = verificationRepository.GetByToken(token);
    if (record == null)
    {
        return Results.BadRequest("Unknown verification token.");
    }

    if (record.VerifiedAtUtc != null)
    {
        return Results.Ok("Email already verified.");
    }

    if (record.ExpiresAtUtc <= DateTime.UtcNow)
    {
        return Results.BadRequest("Verification token expired.");
    }

    accountRepository.MarkEmailVerified(record.AccountId);
    verificationRepository.MarkVerified(record.VerificationId, DateTime.UtcNow);
    return Results.Ok("Email verified. You may now log in.");
});

app.MapPost("/api/login", (LoginRequest request, IAccountRepository accountRepository, SessionManager sessionManager, PasswordHasher hasher) =>
{
    var account = accountRepository.GetByEmail(request.Email);
    if (account == null)
    {
        return Results.Unauthorized();
    }

    if (!account.EmailVerified)
    {
        return Results.Forbid();
    }

    if (!hasher.VerifyPassword(request.Password, account.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var session = sessionManager.IssueSession(account.AccountId);
    var loginToken = sessionManager.IssueLoginToken(account.AccountId);

    return Results.Ok(new AuthResult(account.AccountId, session.SessionId, session.ExpiresAt, loginToken));
});

app.MapPost("/api/sessions", (CreateGameSessionRequest request, IAccountRepository accountRepository, SessionManager sessionManager, GameSessionService gameSessionService) =>
{
    if (!sessionManager.TryGetSession(request.SessionId, out var session) || session.PlayerId != request.AccountId)
    {
        return Results.Unauthorized();
    }

    var account = accountRepository.GetById(request.AccountId);
    if (account == null)
    {
        return Results.NotFound("Account not found");
    }

    var gameSession = gameSessionService.CreateSession(request.AccountId, request.DungeonId);
    return Results.Ok(gameSession);
});

app.MapPost("/api/sessions/{id}/join", (string id, AuthorizedSessionRequest request, SessionManager sessionManager, GameSessionService service) =>
{
    if (!AuthorizeSession(request, sessionManager))
    {
        return Results.Unauthorized();
    }

    var result = service.JoinSession(id, request.AccountId);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/sessions/{id}/leave", (string id, AuthorizedSessionRequest request, SessionManager sessionManager, GameSessionService service) =>
{
    if (!AuthorizeSession(request, sessionManager))
    {
        return Results.Unauthorized();
    }

    var result = service.LeaveSession(id, request.AccountId);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/sessions/{id}/save", (string id, SaveSessionRequest request, SessionManager sessionManager, GameSessionService service) =>
{
    if (!AuthorizeSession(request, sessionManager))
    {
        return Results.Unauthorized();
    }

    var result = service.SaveSession(id, request.AccountId, request.SavedStateJson);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/sessions/{id}/resume", (string id, AuthorizedSessionRequest request, SessionManager sessionManager, GameSessionService service) =>
{
    if (!AuthorizeSession(request, sessionManager))
    {
        return Results.Unauthorized();
    }

    var result = service.ResumeSession(id, request.AccountId);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/health", (RuntimeState state) => Results.Ok(new { status = "ok", startedAt = state.StartedAt }));

app.MapGet("/ready", (RuntimeState state, MariaDbConnectionFactory connectionFactory) =>
{
    if (!state.Ready)
    {
        return Results.Json(new { status = "starting", reason = state.NotReadyReason }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unavailable", reason = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

static bool AuthorizeSession(AuthorizedSessionRequest request, SessionManager sessionManager)
{
    if (!sessionManager.TryGetSession(request.SessionId, out var session))
    {
        return false;
    }

    return session.PlayerId == request.AccountId;
}

static async Task SendVerificationEmailAsync(SmtpOptions options, string recipient, string token)
{
    if (string.IsNullOrWhiteSpace(options.SmtpHost))
    {
        return;
    }

    var verificationUrl = $"{options.VerificationBaseUrl.TrimEnd('/')}/verify?token={token}";
    var body = new StringBuilder();
    body.AppendLine("Welcome to Adventure 2.0!");
    body.AppendLine("Click the link below to verify your email and finish registration:");
    body.AppendLine(verificationUrl);

    using var client = new SmtpClient(options.SmtpHost, options.SmtpPort)
    {
        EnableSsl = options.UseSsl,
        Credentials = new System.Net.NetworkCredential(options.SmtpUser, options.SmtpPassword)
    };

    var message = new MailMessage(options.Sender ?? "noreply@adventure.local", recipient)
    {
        Subject = "Verify your Adventure 2.0 account",
        Body = body.ToString()
    };

    await client.SendMailAsync(message);
}

static LogLevel TryParseLogLevel(string level)
{
    return Enum.TryParse<LogLevel>(level, true, out var parsed) ? parsed : LogLevel.Information;
}

static string ResolveDirectory(string start, string relative)
{
    var current = new DirectoryInfo(start);
    while (current != null)
    {
        var candidate = Path.Combine(current.FullName, relative);
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return Path.GetFullPath(Path.Combine(start, relative));
}

static void ApplyMigrationsAndSeed(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var scoped = scope.ServiceProvider;

    var state = scoped.GetRequiredService<RuntimeState>();
    state.MarkNotReady("Applying migrations");

    var migrator = scoped.GetRequiredService<IMigrationBootstrapper>();
    migrator.Bootstrap();

    var seeder = scoped.GetRequiredService<IReferenceDataSeeder>();
    seeder.SeedReferenceData();

    state.MarkReady();
}

public record RegisterRequest(string Email, string DisplayName, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResult(string AccountId, string SessionId, DateTimeOffset ExpiresAt, string LoginToken);
public record AuthorizedSessionRequest(string AccountId, string SessionId);
public record CreateGameSessionRequest(string AccountId, string SessionId, string? DungeonId);
public record SaveSessionRequest(string AccountId, string SessionId, string SavedStateJson) : AuthorizedSessionRequest(AccountId, SessionId);

public class PasswordHasher
{
    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt.Concat(hash).ToArray());
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        var bytes = Convert.FromBase64String(storedHash);
        var salt = bytes.Take(16).ToArray();
        var hash = bytes.Skip(16).ToArray();
        var attempted = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return attempted.SequenceEqual(hash);
    }
}

public class SmtpOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string VerificationBaseUrl { get; set; } = "http://localhost:5000";
    public string Sender { get; set; } = "noreply@adventure.local";
}
