using Adventure.Server.Core.Lobby;
using Adventure.Server.Core.Repositories;
using Adventure.Server.Core.Sessions;
using Adventure.Server.Persistence;
using Adventure.Server.Persistence.MariaDb;

namespace Adventure.Server.Core.Configuration
{
    public static class ServerServiceConfigurator
    {
        public static ServiceRegistry CreateMariaDbBackedServices(string connectionString, string schemaDirectory)
        {
            var services = new ServiceRegistry();

            services.AddInstance(new MariaDbConnectionFactory(connectionString));
            services.AddSingleton<IMigrationBootstrapper>(sp => new MariaDbMigrationBootstrapper(sp.Get<MariaDbConnectionFactory>(), schemaDirectory));
            services.AddSingleton<IAccountRepository>(sp => new MariaDbAccountRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<ICharacterRepository>(sp => new MariaDbCharacterRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<IInventoryRepository>(sp => new MariaDbInventoryRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<IUnlockRepository>(sp => new MariaDbUnlockRepository(sp.Get<MariaDbConnectionFactory>()));
            services.AddSingleton<Adventure.Server.Persistence.IDungeonRunRepository>(sp => new MariaDbDungeonRunRepository(sp.Get<MariaDbConnectionFactory>()));

            services.AddSingleton<IPlayerProfileRepository>(sp => new AccountProfileRepository(sp.Get<IAccountRepository>()));
            services.AddSingleton<IDungeonRunRepository>(sp => new DungeonRunRepositoryAdapter(sp.Get<Adventure.Server.Persistence.IDungeonRunRepository>()));
            services.AddSingleton<ILoginTokenRepository>(_ => new InMemoryLoginTokenRepository());
            services.AddSingleton<ISessionRepository>(_ => new InMemorySessionRepository());
            services.AddSingleton<IPartyRepository>(_ => new InMemoryPartyRepository());
            services.AddSingleton<IChatHistoryRepository>(_ => new InMemoryChatHistoryRepository());

            services.AddSingleton(sp => new SessionManager(sp.Get<ILoginTokenRepository>(), sp.Get<ISessionRepository>()));
            services.AddSingleton(sp => new LobbyManager(sp.Get<IPlayerProfileRepository>(), sp.Get<IPartyRepository>(), sp.Get<IChatHistoryRepository>()));

            return services;
        }
    }
}
