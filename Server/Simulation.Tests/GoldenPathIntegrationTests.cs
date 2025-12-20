using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Adventure.Server.Core.Dungeons;
using Adventure.Server.Core.Lobby;
using Adventure.Server.Core.Repositories;
using Adventure.Server.Simulation;
using Adventure.Shared.Network.Messages;
using Xunit;

namespace Adventure.Server.Simulation.Tests
{
    public class GoldenPathIntegrationTests
    {
        [Fact]
        public async Task LoginLobbyPartyDungeonAndClearFirstRoom()
        {
            var partyRepository = new InMemoryPartyRepository();
            var chatRepository = new InMemoryChatHistoryRepository();
            var playerProfiles = new TestPlayerProfileRepository();
            var lobbyManager = new LobbyManager(playerProfiles, partyRepository, chatRepository);

            var playerA = "player-a";
            var playerB = "player-b";
            lobbyManager.UpsertPresence(playerA);
            lobbyManager.UpsertPresence(playerB);

            var party = lobbyManager.CreateParty(playerA);
            lobbyManager.InviteToParty(party.PartyId, playerA, playerB);
            lobbyManager.AcceptInvite(playerB, party.PartyId);

            var loop = new SimulationLoop();
            await loop.StartAsync();

            try
            {
                var abilityCatalog = new AbilityCatalog();
                abilityCatalog.Register(new AbilityDefinition
                {
                    Id = "test-strike",
                    Range = 10f,
                    Cooldown = TimeSpan.Zero,
                    CastTime = TimeSpan.Zero,
                    Timing = AbilityTiming.Instant,
                    Cost = new AbilityCost { ResourceType = AbilityResourceType.Mana, Amount = 0 },
                    Power = 1000f
                });

                var enemyCatalog = new EnemyArchetypeCatalog();
                enemyCatalog.Register(new EnemyArchetypeDefinition
                {
                    Id = "training-dummy",
                    DisplayName = "Training Dummy",
                    Stats = new StatSnapshot { MaxHealth = 50f, MaxMana = 0 }
                });

                var runRepository = new InMemoryDungeonRunRepository();
                var factory = new DungeonSimulationFactory(loop, abilityCatalog, enemyCatalog, runRepository);
                var instanceManager = new DungeonInstanceManager(partyRepository, factory);

                var instance = instanceManager.StartDungeonForParty(party.PartyId, "test-dungeon");

                await instance.RouteEventAsync(playerA, new AbilityCastRequest
                {
                    AbilityId = "test-strike",
                    TargetId = "enemy-1",
                    TargetPosition = Vector3.Zero
                });

                await WaitForAsync(() =>
                {
                    var run = runRepository.Runs.FirstOrDefault();
                    if (run == null)
                    {
                        return false;
                    }

                    var events = runRepository.GetEvents(run.RunId);
                    return events.Any(evt => evt.EventType == DungeonRunEventTypes.RoomCleared);
                });

                var recordedRun = runRepository.Runs.First();
                var replay = new DungeonRunReplay(recordedRun, runRepository.GetEvents(recordedRun.RunId));
                Assert.NotNull(replay.GetSeed());
            }
            finally
            {
                await loop.StopAsync();
            }
        }

        private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000, int pollMs = 50)
        {
            var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < timeoutAt)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(pollMs);
            }

            throw new TimeoutException("Timed out waiting for dungeon to clear the first room.");
        }

        private class TestPlayerProfileRepository : IPlayerProfileRepository
        {
            public PlayerProfile GetProfile(string playerId)
            {
                return new PlayerProfile(playerId, $"Player {playerId}", 1);
            }
        }
    }
}
