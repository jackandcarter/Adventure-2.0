using Adventure.Shared.Network.Messages;

namespace Adventure.Server.Simulation
{
    public abstract record SimulationOutput;

    public record MovementOutput(string ActorId, MovementCommand Command) : SimulationOutput;

    public record AbilityCastOutput(
        string ActorId,
        AbilityCastResult Result,
        string? SessionId,
        string? RequestId) : SimulationOutput;

    public record CombatOutput(CombatEvent Event) : SimulationOutput;

    public record AbilityCastInput(AbilityCastCommand Command, string? SessionId, string? RequestId);
}
