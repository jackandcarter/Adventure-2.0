using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Adventure.Server.Core.Lobby;
using Adventure.Server.Core.Repositories;

namespace Adventure.Server.Core.Dungeons
{
    public class DungeonInstance
    {
        private readonly IDungeonSimulation simulation;
        private readonly CancellationTokenSource cts = new();

        public string InstanceId { get; }
        public string DungeonId { get; }
        public string PartyId { get; }
        public IReadOnlyCollection<LobbyMemberPresence> Members => members;

        private readonly List<LobbyMemberPresence> members = new();

        public DungeonInstance(string instanceId, string dungeonId, string partyId, IEnumerable<LobbyMemberPresence> partyMembers, IDungeonSimulation simulation)
        {
            InstanceId = instanceId;
            DungeonId = dungeonId;
            PartyId = partyId;
            this.simulation = simulation;
            members.AddRange(partyMembers);
        }

        public Task StartAsync()
        {
            return simulation.RunAsync(InstanceId, DungeonId, members, cts.Token);
        }

        public Task RouteEventAsync(string playerId, object evt)
        {
            return simulation.HandlePlayerEventAsync(InstanceId, playerId, evt, cts.Token);
        }

        public void Stop()
        {
            cts.Cancel();
        }
    }

    /// <summary>
    /// Supervises active dungeon runs and abstracts the lifecycle from the network/transport layers.
    /// </summary>
    public class DungeonInstanceManager
    {
        private readonly ConcurrentDictionary<string, DungeonInstance> instances = new();
        private readonly IPartyRepository partyRepository;
        private readonly IDungeonSimulationFactory simulationFactory;

        public DungeonInstanceManager(IPartyRepository partyRepository, IDungeonSimulationFactory simulationFactory)
        {
            this.partyRepository = partyRepository;
            this.simulationFactory = simulationFactory;
        }

        public DungeonInstance StartDungeonForParty(string partyId, string dungeonId)
        {
            var party = partyRepository.GetParty(partyId);
            if (party == null)
            {
                throw new InvalidOperationException($"Party {partyId} not found");
            }

            var instanceId = Guid.NewGuid().ToString("N");
            var simulation = simulationFactory.Create(dungeonId);
            var instance = new DungeonInstance(instanceId, dungeonId, partyId, party.Members.Values, simulation);
            instances[instanceId] = instance;
            _ = instance.StartAsync();
            return instance;
        }

        public bool TryGetInstance(string instanceId, out DungeonInstance instance)
        {
            return instances.TryGetValue(instanceId, out instance!);
        }

        public bool RouteEvent(string instanceId, string playerId, object evt)
        {
            if (!instances.TryGetValue(instanceId, out var instance))
            {
                return false;
            }

            _ = instance.RouteEventAsync(playerId, evt);
            return true;
        }

        public void StopInstance(string instanceId)
        {
            if (instances.TryRemove(instanceId, out var instance))
            {
                instance.Stop();
            }
        }

        public IReadOnlyCollection<DungeonInstance> ActiveInstances => instances.Values.ToArray();
    }
}
