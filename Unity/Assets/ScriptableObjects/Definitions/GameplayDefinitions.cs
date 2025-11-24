using System;
using System.Collections.Generic;
using Adventure.Shared.Network.Messages;
using UnityEngine;

namespace Adventure.ScriptableObjects
{
    public interface IExportableDefinition
    {
        string Id { get; }

        string DisplayName { get; }

        void Validate(List<string> errors);

        object ToExportModel();
    }

    public abstract class IdentifiedDefinition : ScriptableObject, IExportableDefinition
    {
        [SerializeField]
        [Tooltip("Unique identifier consumed by the server.")]
        private string id = string.Empty;

        [SerializeField]
        [Tooltip("Human friendly label used in tooling and UI.")]
        private string displayName = string.Empty;

        public string Id => id;

        public string DisplayName => displayName;

        public virtual void Validate(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{name}: Definition id is required.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add($"{name}: Display name is required.");
            }
        }

        public abstract object ToExportModel();
    }

    [Serializable]
    public class BaseStats
    {
        public int Level = 1;
        public float MaxHealth = 100f;
        public float MaxMana = 50f;
        public float AttackPower = 10f;
        public float MagicPower = 10f;
        public float Speed = 1f;
        public float Defense = 0f;
        public float MagicResist = 0f;
        public float Tenacity = 0f;
        public float Precision = 0f;
        public float Awareness = 0f;
        public float CritRating = 0f;
        public float EvadeRating = 0f;
        public float TranceGenerationBonus = 0f;
        public float TranceSpendEfficiency = 1f;

        public void Validate(string owner, List<string> errors)
        {
            if (Level <= 0)
            {
                errors.Add($"{owner}: Level must be positive.");
            }

            if (MaxHealth <= 0)
            {
                errors.Add($"{owner}: Max health must be positive.");
            }

            if (MaxMana < 0)
            {
                errors.Add($"{owner}: Max mana cannot be negative.");
            }

            if (Speed <= 0)
            {
                errors.Add($"{owner}: Speed must be positive.");
            }

            if (TranceSpendEfficiency <= 0)
            {
                errors.Add($"{owner}: Trance spend efficiency must be positive.");
            }
        }
    }

    [Serializable]
    public enum AbilityTiming
    {
        Instant,
        Cast,
        Channel
    }

    [Serializable]
    public enum AbilityResourceType
    {
        Mana,
        Trance
    }

    [Serializable]
    public enum Element
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Arcane,
        Holy,
        Shadow
    }

    [Serializable]
    public class AbilityCost
    {
        public AbilityResourceType ResourceType = AbilityResourceType.Mana;
        public float Amount = 10f;

        public void Validate(string owner, List<string> errors)
        {
            if (Amount < 0)
            {
                errors.Add($"{owner}: Ability cost cannot be negative.");
            }
        }
    }

    [CreateAssetMenu(menuName = "Adventure/Definitions/Ability", fileName = "AbilityDefinition")]
    public class AbilityDefinitionAsset : IdentifiedDefinition
    {
        [SerializeField]
        private string description = string.Empty;

        [SerializeField]
        private float range = 25f;

        [SerializeField]
        [Tooltip("Cooldown in seconds.")]
        private float cooldownSeconds = 5f;

        [SerializeField]
        [Tooltip("Cast time in seconds. Only applicable for cast/channel abilities.")]
        private float castTimeSeconds = 0f;

        [SerializeField]
        [Tooltip("Channel duration in seconds. Only applicable for channel abilities.")]
        private float channelDurationSeconds = 0f;

        [SerializeField]
        private AbilityTiming timing = AbilityTiming.Instant;

        [SerializeField]
        private bool requiresLineOfSight = true;

        [SerializeField]
        private AbilityCost cost = new();

        [SerializeField]
        private Element element = Element.Physical;

        [SerializeField]
        private float power = 1f;

        [SerializeField]
        private bool isHealing;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (range <= 0)
            {
                errors.Add($"{name}: Range must be positive.");
            }

            if (cooldownSeconds < 0)
            {
                errors.Add($"{name}: Cooldown cannot be negative.");
            }

            if (castTimeSeconds < 0)
            {
                errors.Add($"{name}: Cast time cannot be negative.");
            }

            if (channelDurationSeconds < 0)
            {
                errors.Add($"{name}: Channel duration cannot be negative.");
            }

            if (timing == AbilityTiming.Instant && (castTimeSeconds > 0 || channelDurationSeconds > 0))
            {
                errors.Add($"{name}: Instant abilities must have zero cast/channel duration.");
            }

            cost.Validate(name, errors);
            if (power <= 0)
            {
                errors.Add($"{name}: Power must be positive.");
            }
        }

        public override object ToExportModel()
        {
            return new AbilityDefinitionPayload
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = description,
                Range = range,
                CooldownSeconds = cooldownSeconds,
                CastTimeSeconds = castTimeSeconds,
                ChannelDurationSeconds = channelDurationSeconds,
                Timing = timing,
                RequiresLineOfSight = requiresLineOfSight,
                Cost = new AbilityCostPayload { ResourceType = cost.ResourceType, Amount = cost.Amount },
                Element = element,
                Power = power,
                IsHealing = isHealing
            };
        }
    }

    [Serializable]
    public enum StatusStackBehavior
    {
        Refresh,
        Extend,
        Stack
    }

    [Flags]
    [Serializable]
    public enum StatusFlags
    {
        None = 0,
        IsBuff = 1 << 0,
        IsDebuff = 1 << 1,
        CrowdControl = 1 << 2,
        Silence = 1 << 3,
        Root = 1 << 4
    }

    [Serializable]
    public enum DispelType
    {
        Magic,
        Curse,
        Poison,
        Physical
    }

    [CreateAssetMenu(menuName = "Adventure/Definitions/Status Effect", fileName = "StatusEffectDefinition")]
    public class StatusEffectDefinitionAsset : IdentifiedDefinition
    {
        [SerializeField]
        private float durationSeconds = 5f;

        [SerializeField]
        private float periodicIntervalSeconds = 0f;

        [SerializeField]
        private int maxStacks = 1;

        [SerializeField]
        private StatusStackBehavior stackBehavior = StatusStackBehavior.Refresh;

        [SerializeField]
        private StatusFlags flags = StatusFlags.None;

        [SerializeField]
        private List<DispelType> dispelTypes = new();

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (durationSeconds <= 0)
            {
                errors.Add($"{name}: Duration must be positive.");
            }

            if (periodicIntervalSeconds < 0)
            {
                errors.Add($"{name}: Periodic interval cannot be negative.");
            }

            if (maxStacks <= 0)
            {
                errors.Add($"{name}: Max stacks must be positive.");
            }
        }

        public override object ToExportModel()
        {
            return new StatusEffectDefinitionPayload
            {
                Id = Id,
                DisplayName = DisplayName,
                DurationSeconds = durationSeconds,
                PeriodicIntervalSeconds = periodicIntervalSeconds > 0 ? periodicIntervalSeconds : (float?)null,
                MaxStacks = maxStacks,
                StackBehavior = stackBehavior,
                Flags = flags,
                DispelTypes = dispelTypes
            };
        }
    }

    [CreateAssetMenu(menuName = "Adventure/Definitions/Player Class", fileName = "ClassDefinition")]
    public class PlayerClassDefinition : IdentifiedDefinition
    {
        [SerializeField]
        [Tooltip("Base statline that seeds player resources when the class is selected.")]
        private BaseStats baseStats = new();

        [SerializeField]
        [Tooltip("Ability ids granted at creation.")]
        private List<string> startingAbilities = new();

        [SerializeField]
        [Tooltip("Status effect ids applied while the class is active.")]
        private List<string> passiveStatusEffects = new();

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            baseStats.Validate(name, errors);
            if (startingAbilities.Count == 0)
            {
                errors.Add($"{name}: At least one starting ability is required.");
            }
        }

        public override object ToExportModel()
        {
            return new PlayerClassPayload
            {
                Id = Id,
                DisplayName = DisplayName,
                BaseStats = baseStats,
                StartingAbilities = startingAbilities,
                PassiveStatusEffects = passiveStatusEffects
            };
        }
    }

    [Serializable]
    public class LootEntry
    {
        public string ItemId = string.Empty;
        public int Weight = 1;
        public int MinQuantity = 1;
        public int MaxQuantity = 1;

        public void Validate(string owner, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(ItemId))
            {
                errors.Add($"{owner}: Loot entries must specify an item id.");
            }

            if (Weight <= 0)
            {
                errors.Add($"{owner}: Loot weight must be positive.");
            }

            if (MinQuantity <= 0 || MaxQuantity <= 0 || MaxQuantity < MinQuantity)
            {
                errors.Add($"{owner}: Loot quantities must be positive and MaxQuantity >= MinQuantity.");
            }
        }
    }

    [CreateAssetMenu(menuName = "Adventure/Definitions/Loot Table", fileName = "LootTableDefinition")]
    public class LootTableDefinition : IdentifiedDefinition
    {
        [SerializeField]
        private List<LootEntry> entries = new();

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (entries.Count == 0)
            {
                errors.Add($"{name}: Loot table requires at least one entry.");
                return;
            }

            foreach (var entry in entries)
            {
                entry.Validate(name, errors);
            }
        }

        public override object ToExportModel()
        {
            return new LootTablePayload
            {
                Id = Id,
                DisplayName = DisplayName,
                Entries = entries
            };
        }
    }

    [CreateAssetMenu(menuName = "Adventure/Definitions/Enemy Archetype", fileName = "EnemyArchetypeDefinition")]
    public class EnemyArchetypeDefinition : IdentifiedDefinition
    {
        [SerializeField]
        private BaseStats stats = new();

        [SerializeField]
        private List<string> abilityIds = new();

        [SerializeField]
        private LootTableDefinition? lootTable;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            stats.Validate(name, errors);
            if (abilityIds.Count == 0)
            {
                errors.Add($"{name}: Enemy archetype requires at least one ability.");
            }

            if (lootTable == null)
            {
                errors.Add($"{name}: Loot table is required for enemy archetypes.");
            }
        }

        public override object ToExportModel()
        {
            return new EnemyArchetypePayload
            {
                Id = Id,
                DisplayName = DisplayName,
                Stats = stats,
                AbilityIds = abilityIds,
                LootTableId = lootTable != null ? lootTable.Id : string.Empty
            };
        }
    }

    [Serializable]
    public class SpawnPointDefinition
    {
        public string Id = string.Empty;
        public Vector3 Position = Vector3.zero;
        public string Tag = string.Empty;

        public void Validate(string owner, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                errors.Add($"{owner}: Spawn points must define an id.");
            }

            if (string.IsNullOrWhiteSpace(Tag))
            {
                errors.Add($"{owner}: Spawn points should specify a tag to filter eligible actors.");
            }
        }
    }

    [Serializable]
    public class DoorDefinition
    {
        public string DoorId = string.Empty;
        public string SocketId = string.Empty;
        public string RequiredKeyId = string.Empty;
        public bool StartsLocked = false;
        public bool IsOneWay = false;

        public void Validate(string owner, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(DoorId))
            {
                errors.Add($"{owner}: Doors require an id.");
            }

            if (string.IsNullOrWhiteSpace(SocketId))
            {
                errors.Add($"{owner}: Doors must reference a socket id.");
            }
        }
    }

    [Serializable]
    public class TriggerDefinition
    {
        public string TriggerId = string.Empty;
        public List<string> RequiredTriggers = new();
        public string ActivatesStateId = string.Empty;
        public bool ServerOnly = false;

        public void Validate(string owner, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(TriggerId))
            {
                errors.Add($"{owner}: Triggers require an id.");
            }
        }
    }

    [Serializable]
    public class InteractiveObjectDefinition
    {
        public string ObjectId = string.Empty;
        public string Kind = string.Empty;
        public string GrantsKeyId = string.Empty;
        public string RequiresKeyId = string.Empty;
        public string ActivatesTriggerId = string.Empty;

        public void Validate(string owner, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(ObjectId))
            {
                errors.Add($"{owner}: Interactive objects require an id.");
            }

            if (string.IsNullOrWhiteSpace(Kind))
            {
                errors.Add($"{owner}: Interactive objects require a kind.");
            }
        }
    }

    [Serializable]
    public class EnvironmentStateDefinition
    {
        public string StateId = string.Empty;
        public string DefaultValue = string.Empty;

        public void Validate(string owner, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(StateId))
            {
                errors.Add($"{owner}: Environment states require an id.");
            }

            if (string.IsNullOrWhiteSpace(DefaultValue))
            {
                errors.Add($"{owner}: Environment states require a default value.");
            }
        }
    }

    [CreateAssetMenu(menuName = "Adventure/Definitions/Room Template", fileName = "RoomTemplateDefinition")]
    public class RoomTemplateDefinition : IdentifiedDefinition
    {
        [SerializeField]
        private RoomTemplateType roomType = RoomTemplateType.Combat;

        [SerializeField]
        [Tooltip("Optional prefab used for authoring.")]
        private GameObject? roomPrefab;

        [SerializeField]
        private List<SpawnPointDefinition> spawnPoints = new();

        [SerializeField]
        private List<DoorDefinition> doors = new();

        [SerializeField]
        private List<TriggerDefinition> triggers = new();

        [SerializeField]
        private List<InteractiveObjectDefinition> interactiveObjects = new();

        [SerializeField]
        private List<string> providesKeys = new();

        [SerializeField]
        private List<EnvironmentStateDefinition> environmentStates = new();

        public GameObject? RoomPrefab => roomPrefab;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            foreach (var spawnPoint in spawnPoints)
            {
                spawnPoint.Validate(name, errors);
            }

            foreach (var door in doors)
            {
                door.Validate(name, errors);
            }

            foreach (var trigger in triggers)
            {
                trigger.Validate(name, errors);
            }

            foreach (var interactive in interactiveObjects)
            {
                interactive.Validate(name, errors);
            }

            foreach (var state in environmentStates)
            {
                state.Validate(name, errors);
            }

            if (roomPrefab == null)
            {
                errors.Add($"{name}: Room prefab is recommended to validate sockets, doors, and triggers.");
            }
#if UNITY_EDITOR
            else
            {
                Adventure.EditorTools.RoomPrefabValidator.Validate(roomPrefab, errors);
            }
#endif
        }

        public override object ToExportModel()
        {
            return new RoomTemplatePayload
            {
                TemplateId = Id,
                DisplayName = DisplayName,
                RoomType = roomType,
                SpawnPoints = spawnPoints,
                Doors = doors,
                Triggers = triggers,
                InteractiveObjects = interactiveObjects,
                ProvidesKeys = providesKeys,
                EnvironmentStates = environmentStates
            };
        }
    }

    [CreateAssetMenu(menuName = "Adventure/Definitions/Dungeon Theme", fileName = "DungeonThemeDefinition")]
    public class DungeonThemeDefinition : IdentifiedDefinition
    {
        [SerializeField]
        private List<RoomTemplateDefinition> roomTemplates = new();

        [SerializeField]
        [Tooltip("Enemy archetype ids available in this dungeon.")]
        private List<string> enemyArchetypeIds = new();

        [SerializeField]
        [Tooltip("Ability ids that can appear as room objectives or rewards.")]
        private List<string> featuredAbilities = new();

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (roomTemplates.Count == 0)
            {
                errors.Add($"{name}: Dungeon theme requires at least one room template.");
            }

            if (enemyArchetypeIds.Count == 0)
            {
                errors.Add($"{name}: Dungeon theme should list available enemy archetypes.");
            }
        }

        public override object ToExportModel()
        {
            var templateIds = new List<string>();
            foreach (var template in roomTemplates)
            {
                templateIds.Add(template.Id);
            }

            return new DungeonThemePayload
            {
                Id = Id,
                DisplayName = DisplayName,
                RoomTemplateIds = templateIds,
                EnemyArchetypeIds = enemyArchetypeIds,
                FeaturedAbilities = featuredAbilities
            };
        }
    }

    [Serializable]
    public class AbilityCostPayload
    {
        public AbilityResourceType ResourceType { get; set; }
        public float Amount { get; set; }
    }

    [Serializable]
    public class AbilityDefinitionPayload
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float Range { get; set; }
        public float CooldownSeconds { get; set; }
        public float CastTimeSeconds { get; set; }
        public float ChannelDurationSeconds { get; set; }
        public AbilityTiming Timing { get; set; }
        public bool RequiresLineOfSight { get; set; }
        public AbilityCostPayload Cost { get; set; } = new();
        public Element Element { get; set; }
        public float Power { get; set; }
        public bool IsHealing { get; set; }
    }

    [Serializable]
    public class StatusEffectDefinitionPayload
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public float DurationSeconds { get; set; }
        public float? PeriodicIntervalSeconds { get; set; }
        public int MaxStacks { get; set; }
        public StatusStackBehavior StackBehavior { get; set; }
        public StatusFlags Flags { get; set; }
        public List<DispelType> DispelTypes { get; set; } = new();
    }

    [Serializable]
    public class PlayerClassPayload
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public BaseStats BaseStats { get; set; } = new();
        public List<string> StartingAbilities { get; set; } = new();
        public List<string> PassiveStatusEffects { get; set; } = new();
    }

    [Serializable]
    public class LootTablePayload
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<LootEntry> Entries { get; set; } = new();
    }

    [Serializable]
    public class EnemyArchetypePayload
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public BaseStats Stats { get; set; } = new();
        public List<string> AbilityIds { get; set; } = new();
        public string LootTableId { get; set; } = string.Empty;
    }

    [Serializable]
    public class RoomTemplatePayload
    {
        public string TemplateId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public RoomTemplateType RoomType { get; set; }
        public List<SpawnPointDefinition> SpawnPoints { get; set; } = new();
        public List<DoorDefinition> Doors { get; set; } = new();
        public List<TriggerDefinition> Triggers { get; set; } = new();
        public List<InteractiveObjectDefinition> InteractiveObjects { get; set; } = new();
        public List<string> ProvidesKeys { get; set; } = new();
        public List<EnvironmentStateDefinition> EnvironmentStates { get; set; } = new();
    }

    [Serializable]
    public class DungeonThemePayload
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> RoomTemplateIds { get; set; } = new();
        public List<string> EnemyArchetypeIds { get; set; } = new();
        public List<string> FeaturedAbilities { get; set; } = new();
    }
}
