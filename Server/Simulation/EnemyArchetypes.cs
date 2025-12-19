using System.Collections.Generic;

namespace Adventure.Server.Simulation
{
    public class EnemyArchetypeDefinition
    {
        public string Id { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public StatSnapshot Stats { get; init; } = new();

        public IReadOnlyList<string> AbilityIds { get; init; } = new List<string>();

        public string LootTableId { get; init; } = string.Empty;
    }

    public class EnemyArchetypeCatalog
    {
        private readonly Dictionary<string, EnemyArchetypeDefinition> definitions = new();

        public void Register(params EnemyArchetypeDefinition[] archetypes)
        {
            foreach (var archetype in archetypes)
            {
                definitions[archetype.Id] = archetype;
            }
        }

        public bool TryGet(string archetypeId, out EnemyArchetypeDefinition definition)
        {
            return definitions.TryGetValue(archetypeId, out definition!);
        }
    }
}
