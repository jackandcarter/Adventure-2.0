using System.Threading.Tasks;
using Adventure.Server.Core.Dungeons;
using Adventure.Server.Network;
using Adventure.Shared.Network.Messages;
using Microsoft.Extensions.Logging;

namespace Adventure.Server.Host
{
    public class DungeonMessageHandlers
    {
        private readonly DungeonInstanceManager dungeonInstances;
        private readonly ILogger<DungeonMessageHandlers> logger;

        public DungeonMessageHandlers(DungeonInstanceManager dungeonInstances, ILogger<DungeonMessageHandlers> logger)
        {
            this.dungeonInstances = dungeonInstances;
            this.logger = logger;
        }

        public void Register(MessageRouter router)
        {
            router.RegisterHandler<MovementCommand>(MessageTypes.Movement, ctx =>
                RouteToDungeonAsync(ctx.PlayerId, new PlayerEventEnvelope(ctx.Envelope.SessionId, ctx.Envelope.RequestId, ctx.Sender, ctx.Payload ?? new MovementCommand())));

            router.RegisterHandler<AbilityCastRequest>(MessageTypes.AbilityCast, ctx =>
                RouteToDungeonAsync(ctx.PlayerId, new PlayerEventEnvelope(ctx.Envelope.SessionId, ctx.Envelope.RequestId, ctx.Sender, ctx.Payload ?? new AbilityCastRequest())));
        }

        private async Task RouteToDungeonAsync(string? playerId, PlayerEventEnvelope envelope)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (!dungeonInstances.TryGetInstanceForPlayer(playerId, out var instance))
            {
                logger.LogWarning("Received dungeon input for {PlayerId} with no active instance.", playerId);
                await envelope.Sender.SendAsync(new MessageEnvelope<ErrorResponse>
                {
                    Type = MessageTypes.Error,
                    SessionId = envelope.SessionId,
                    Payload = new ErrorResponse
                    {
                        Code = "not_in_dungeon",
                        Message = "Player is not in an active dungeon."
                    }
                });
                return;
            }

            await instance.RouteEventAsync(playerId, envelope);
        }
    }
}
