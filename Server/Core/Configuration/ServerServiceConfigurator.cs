using Adventure.Server.Core.Dungeons;
using Adventure.Server.Core.Lobby;
using Adventure.Server.Core.Repositories;
using Adventure.Server.Core.Sessions;
using Adventure.Server.Persistence.MariaDb;
using Adventure.Server.Persistence;
using Adventure.Server.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace Adventure.Server.Core.Configuration
{
    public static class ServerServiceConfigurator
    {
        public static ServiceRegistry CreateMariaDbBackedServices(string connectionString, string migrationDirectory)
        {
            var services = new ServiceRegistry();

            services.AddInstance(new MariaDbConnectionFactory(connectionString));
            services.AddSingleton<IMigrationBootstrapper>(sp => new MariaDbMigrationBootstrapper(sp.Get<MariaDbConnectionFactory>(), migrationDirectory));
            services.AddSingleton<IAccountRepository>(sp => new MariaDbAccountRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<ICharacterRepository>(sp => new MariaDbCharacterRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<IInventoryRepository>(sp => new MariaDbInventoryRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<IUnlockRepository>(sp => new MariaDbUnlockRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<IDungeonRunRepository>(sp => new MariaDbDungeonRunRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<ILoginTokenRepository>(sp => new MariaDbLoginTokenRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<ISessionRepository>(sp => new MariaDbSessionRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<IEmailVerificationRepository>(sp => new MariaDbEmailVerificationRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<IGameSessionRepository>(sp => new MariaDbGameSessionRepository(sp.Get<MariaDbConnectionFactory>()));

            services.AddSingleton<IPlayerProfileRepository>(sp => new AccountProfileRepository(sp.Get<IAccountRepository>()));
            services.AddSingleton<IDungeonRunRepository>(sp => new DungeonRunRepositoryAdapter(sp.Get<Adventure.Server.Persistence.IDungeonRunRepository>()));
            services.AddSingleton<IPartyRepository>(_ => new InMemoryPartyRepository());
            services.AddSingleton<IChatHistoryRepository>(_ => new InMemoryChatHistoryRepository());

            services.AddSingleton(sp => new SessionManager(sp.Get<ILoginTokenRepository>(), sp.Get<ISessionRepository>()));
            services.AddSingleton(sp => new LobbyManager(sp.Get<IPlayerProfileRepository>(), sp.Get<IPartyRepository>(), sp.Get<IChatHistoryRepository>()));
            services.AddSingleton(sp => new GameSessionService(sp.Get<IGameSessionRepository>()));
            services.AddSingleton(_ => new SimulationLoop());
            services.AddSingleton(sp => new DungeonSimulationFactory(sp.Get<SimulationLoop>(), sp.Get<AbilityCatalog>(), sp.Get<EnemyArchetypeCatalog>(), sp.Get<IDungeonRunRepository>()));
            services.AddSingleton(sp => new DungeonInstanceManager(sp.Get<IPartyRepository>(), sp.Get<IDungeonSimulationFactory>()));

            return services;
        }

        public static IServiceCollection ConfigureServices(IServiceCollection services, string connectionString, string migrationDirectory)
        {
            services.AddSingleton(new MariaDbConnectionFactory(connectionString));
            services.AddSingleton<IMigrationBootstrapper>(sp =>
                new MariaDbMigrationBootstrapper(sp.GetRequiredService<MariaDbConnectionFactory>(), migrationDirectory));
            services.AddSingleton<IReferenceDataSeeder, MariaDbReferenceDataSeeder>();

            services.AddSingleton<IAccountRepository, MariaDbAccountRepository>();
            services.AddSingleton<ICharacterRepository, MariaDbCharacterRepository>();
            services.AddSingleton<IInventoryRepository, MariaDbInventoryRepository>();
            services.AddSingleton<IUnlockRepository, MariaDbUnlockRepository>();
            services.AddSingleton<Adventure.Server.Persistence.IDungeonRunRepository, MariaDbDungeonRunRepository>();
            services.AddSingleton<ILoginTokenRepository, MariaDbLoginTokenRepository>();
            services.AddSingleton<ISessionRepository, MariaDbSessionRepository>();
            services.AddSingleton<IEmailVerificationRepository, MariaDbEmailVerificationRepository>();
            services.AddSingleton<IGameSessionRepository, MariaDbGameSessionRepository>();

            services.AddSingleton<IPlayerProfileRepository, AccountProfileRepository>();
            services.AddSingleton<IDungeonRunRepository, DungeonRunRepositoryAdapter>();
            services.AddSingleton<IPartyRepository, InMemoryPartyRepository>();
            services.AddSingleton<IChatHistoryRepository, InMemoryChatHistoryRepository>();

            services.AddSingleton<SessionManager>();
            services.AddSingleton<LobbyManager>();
            services.AddSingleton<GameSessionService>();
            services.AddSingleton<IDungeonSimulationFactory, DungeonSimulationFactory>();
            services.AddSingleton<DungeonInstanceManager>();

            return services;
        }
    }
}
