using System.Collections.Generic;
using UnityEngine;

namespace Adventure.GameData.Definitions
{
    [CreateAssetMenu(menuName = "Adventure/Definitions/Class Definition", fileName = "NewClassDefinition")]
    public class ClassDefinition : ScriptableObject, IIdentifiableDefinition
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private string displayName;

        [TextArea]
        [SerializeField]
        private string description;

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private StatBlock baseStats;

        [SerializeField]
        private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

        [SerializeField]
        private List<string> tags = new List<string>();

        public string Id => id;

        public string DisplayName => displayName;

        public string Description => description;

        public Sprite Icon => icon;

        public StatBlock BaseStats => baseStats;

        public IReadOnlyList<AbilityDefinition> Abilities => abilities;

        public IReadOnlyList<string> Tags => tags;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId;
            displayName = newDisplayName;
        }
    }
}
