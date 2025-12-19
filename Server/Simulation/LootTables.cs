using System.Collections.Generic;

namespace Adventure.Server.Simulation
{
    public class LootEntryDefinition
    {
        public string ItemId { get; init; } = string.Empty;

        public int Weight { get; init; } = 1;

        public int MinQuantity { get; init; } = 1;

        public int MaxQuantity { get; init; } = 1;
    }

    public class LootTableDefinition
    {
        public string Id { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public IReadOnlyList<LootEntryDefinition> Entries { get; init; } = new List<LootEntryDefinition>();
    }

    public class LootTableCatalog
    {
        private readonly Dictionary<string, LootTableDefinition> definitions = new();

        public void Register(params LootTableDefinition[] lootTables)
        {
            foreach (var lootTable in lootTables)
            {
                definitions[lootTable.Id] = lootTable;
            }
        }

        public bool TryGet(string lootTableId, out LootTableDefinition definition)
        {
            return definitions.TryGetValue(lootTableId, out definition!);
        }
    }
}
