using System.Collections.Generic;

namespace Adventure.Server.Simulation
{
    public class SimulationAbilityDataSet
    {
        public List<AbilityDefinitionData> Abilities { get; set; } = new();
    }

    public class AbilityDefinitionData
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public float Range { get; set; } = 25f;

        public float CooldownSeconds { get; set; } = 5f;

        public float CastTimeSeconds { get; set; }

        public float ChannelDurationSeconds { get; set; }

        public AbilityTiming Timing { get; set; } = AbilityTiming.Instant;

        public AbilityCategory Category { get; set; } = AbilityCategory.Standard;

        public bool RequiresLineOfSight { get; set; } = true;

        public AbilityCostData Cost { get; set; } = new();

        public Element Element { get; set; } = Element.Physical;

        public float Power { get; set; } = 1f;

        public bool IsHealing { get; set; }

        public float MinimumTrance { get; set; }

        public TranceBehavior TranceBehavior { get; set; } = TranceBehavior.None;
    }

    public class AbilityCostData
    {
        public AbilityResourceType ResourceType { get; set; } = AbilityResourceType.Mana;

        public float Amount { get; set; } = 10f;
    }

    public class SimulationEnemyArchetypeDataSet
    {
        public List<EnemyArchetypeData> EnemyArchetypes { get; set; } = new();
    }

    public class EnemyArchetypeData
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public StatSnapshot Stats { get; set; } = new();

        public List<string> AbilityIds { get; set; } = new();

        public string LootTableId { get; set; } = string.Empty;
    }

    public class SimulationLootTableDataSet
    {
        public List<LootTableData> LootTables { get; set; } = new();
    }

    public class LootTableData
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public List<LootEntryData> Entries { get; set; } = new();
    }

    public class LootEntryData
    {
        public string ItemId { get; set; } = string.Empty;

        public int Weight { get; set; } = 1;

        public int MinQuantity { get; set; } = 1;

        public int MaxQuantity { get; set; } = 1;
    }

    public class SimulationCatalogs
    {
        public AbilityCatalog Abilities { get; } = new();

        public EnemyArchetypeCatalog EnemyArchetypes { get; } = new();

        public LootTableCatalog LootTables { get; } = new();
    }
}
