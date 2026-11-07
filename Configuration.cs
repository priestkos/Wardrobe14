using Dalamud.Configuration;
using Newtonsoft.Json;

namespace Wardrobe14;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    /// <summary> Extra grid columns besides the player's own collection. </summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<Guid> PinnedCollections { get; set; } = [];
}
