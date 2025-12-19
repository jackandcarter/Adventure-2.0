using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Adventure.Server.Simulation
{
    public static class SimulationDataLoader
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static SimulationCatalogs LoadFromDirectory(string dataDirectory, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
            {
                throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
            }

            var catalogs = new SimulationCatalogs();
            var abilities = LoadAbilityData(Path.Combine(dataDirectory, "abilities.json"), logger);
            var lootTables = LoadLootTables(Path.Combine(dataDirectory, "loot-tables.json"), logger);
            var enemyArchetypes = LoadEnemyArchetypes(Path.Combine(dataDirectory, "enemy-archetypes.json"), logger);

            catalogs.Abilities.Register(abilities.Select(ToAbilityDefinition).ToArray());
            catalogs.LootTables.Register(lootTables.Select(ToLootTableDefinition).ToArray());
            catalogs.EnemyArchetypes.Register(enemyArchetypes.Select(ToEnemyArchetypeDefinition).ToArray());

            return catalogs;
        }

        private static IReadOnlyList<AbilityDefinitionData> LoadAbilityData(string path, ILogger logger)
        {
            var data = LoadJson<SimulationAbilityDataSet>(path, logger);
            return data?.Abilities ?? Array.Empty<AbilityDefinitionData>();
        }

        private static IReadOnlyList<EnemyArchetypeData> LoadEnemyArchetypes(string path, ILogger logger)
        {
            var data = LoadJson<SimulationEnemyArchetypeDataSet>(path, logger);
            return data?.EnemyArchetypes ?? Array.Empty<EnemyArchetypeData>();
        }

        private static IReadOnlyList<LootTableData> LoadLootTables(string path, ILogger logger)
        {
            var data = LoadJson<SimulationLootTableDataSet>(path, logger);
            return data?.LootTables ?? Array.Empty<LootTableData>();
        }

        private static T? LoadJson<T>(string path, ILogger logger)
        {
            if (!File.Exists(path))
            {
                logger.LogWarning("Simulation data file not found: {Path}", path);
                return default;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, Options);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load simulation data from {Path}", path);
                return default;
            }
        }

        private static AbilityDefinition ToAbilityDefinition(AbilityDefinitionData data)
        {
            return new AbilityDefinition
            {
                Id = data.Id,
                Range = data.Range,
                Cooldown = TimeSpan.FromSeconds(data.CooldownSeconds),
                CastTime = TimeSpan.FromSeconds(data.CastTimeSeconds),
                ChannelDuration = TimeSpan.FromSeconds(data.ChannelDurationSeconds),
                Timing = data.Timing,
                Category = data.Category,
                RequiresLineOfSight = data.RequiresLineOfSight,
                Cost = new AbilityCost
                {
                    ResourceType = data.Cost.ResourceType,
                    Amount = data.Cost.Amount
                },
                Element = data.Element,
                Power = data.Power,
                IsHealing = data.IsHealing,
                MinimumTrance = data.MinimumTrance,
                TranceBehavior = data.TranceBehavior
            };
        }

        private static LootTableDefinition ToLootTableDefinition(LootTableData data)
        {
            return new LootTableDefinition
            {
                Id = data.Id,
                DisplayName = data.DisplayName,
                Entries = data.Entries.Select(entry => new LootEntryDefinition
                {
                    ItemId = entry.ItemId,
                    Weight = entry.Weight,
                    MinQuantity = entry.MinQuantity,
                    MaxQuantity = entry.MaxQuantity
                }).ToList()
            };
        }

        private static EnemyArchetypeDefinition ToEnemyArchetypeDefinition(EnemyArchetypeData data)
        {
            return new EnemyArchetypeDefinition
            {
                Id = data.Id,
                DisplayName = data.DisplayName,
                Stats = data.Stats,
                AbilityIds = data.AbilityIds,
                LootTableId = data.LootTableId
            };
        }
    }
}
