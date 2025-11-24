using UnityEngine;

namespace Adventure.GameData.Definitions
{
    public interface IIdentifiableDefinition
    {
        string Id { get; }
        string DisplayName { get; }
        void SetIdentity(string id, string displayName);
    }
}
