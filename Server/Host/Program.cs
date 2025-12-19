using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Adventure.Server.Core.Configuration;
using Adventure.Server.Core.Repositories;
using Adventure.Server.Core.Sessions;
using Adventure.Server.Persistence;
using Adventure.Server.Network;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<PasswordHasher>();

var connectionString = builder.Configuration.GetConnectionString("MariaDb")
    ?? builder.Configuration["Database:ConnectionString"]
    ?? "Server=localhost;Database=adventure;Uid=root;Pwd=pass;";
var schemaDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "db", "schema"));
var smtpOptions = builder.Configuration.GetSection("Mail").Get<SmtpOptions>() ?? new SmtpOptions();

var serviceRegistry = ServerServiceConfigurator.CreateMariaDbBackedServices(connectionString, schemaDirectory);
serviceRegistry.Get<IMigrationBootstrapper>().Bootstrap();

builder.Services.AddSingleton(serviceRegistry);
builder.Services.AddSingleton(smtpOptions);
builder.Services.AddSingleton(new SessionRegistry());
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton(new WebSocketConnectionRegistry());
builder.Services.AddSingleton(new WebSocketListenerOptions());
builder.Services.AddSingleton<WebSocketListener>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

app.Map("/ws", async (HttpContext context, WebSocketListener listener) =>
{
    await listener.AcceptAsync(context);
});

app.MapPost("/api/register", async (RegisterRequest request, ServiceRegistry registry, PasswordHasher hasher, SmtpOptions mailOptions) =>
{
    var accountRepository = registry.Get<IAccountRepository>();
    var verificationRepository = registry.Get<IEmailVerificationRepository>();

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

app.MapGet("/verify", (string token, ServiceRegistry registry) =>
{
    var verificationRepository = registry.Get<IEmailVerificationRepository>();
    var accountRepository = registry.Get<IAccountRepository>();

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

app.MapPost("/api/login", (LoginRequest request, ServiceRegistry registry, PasswordHasher hasher) =>
{
    var accountRepository = registry.Get<IAccountRepository>();
    var sessionManager = registry.Get<SessionManager>();

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

app.MapPost("/api/sessions", (CreateGameSessionRequest request, ServiceRegistry registry) =>
{
    var accountRepository = registry.Get<IAccountRepository>();
    var sessionManager = registry.Get<SessionManager>();
    var gameSessionService = registry.Get<GameSessionService>();

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

app.MapPost("/api/sessions/{id}/join", (string id, AuthorizedSessionRequest request, ServiceRegistry registry) =>
{
    if (!AuthorizeSession(request, registry.Get<SessionManager>()))
    {
        return Results.Unauthorized();
    }

    var service = registry.Get<GameSessionService>();
    var result = service.JoinSession(id, request.AccountId);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/sessions/{id}/leave", (string id, AuthorizedSessionRequest request, ServiceRegistry registry) =>
{
    if (!AuthorizeSession(request, registry.Get<SessionManager>()))
    {
        return Results.Unauthorized();
    }

    var service = registry.Get<GameSessionService>();
    var result = service.LeaveSession(id, request.AccountId);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/sessions/{id}/save", (string id, SaveSessionRequest request, ServiceRegistry registry) =>
{
    if (!AuthorizeSession(request, registry.Get<SessionManager>()))
    {
        return Results.Unauthorized();
    }

    var service = registry.Get<GameSessionService>();
    var result = service.SaveSession(id, request.AccountId, request.SavedStateJson);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapPost("/api/sessions/{id}/resume", (string id, AuthorizedSessionRequest request, ServiceRegistry registry) =>
{
    if (!AuthorizeSession(request, registry.Get<SessionManager>()))
    {
        return Results.Unauthorized();
    }

    var service = registry.Get<GameSessionService>();
    var result = service.ResumeSession(id, request.AccountId);
    return result == null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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
