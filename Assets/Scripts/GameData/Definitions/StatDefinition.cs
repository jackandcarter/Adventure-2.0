using UnityEngine;

namespace Adventure.GameData.Definitions
{
    [CreateAssetMenu(menuName = "Adventure/Definitions/Stat Definition", fileName = "NewStatDefinition")]
    public class StatDefinition : ScriptableObject, IIdentifiableDefinition
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

        public string Id => id;

        public string DisplayName => displayName;

        public string Description => description;

        public Sprite Icon => icon;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId;
            displayName = newDisplayName;
        }
    }
}
