namespace Content.Shared._PV.Terraforming;

[RegisterComponent]
public sealed partial class TerraformerSaplingComponent : Component
{
    /// <summary>
    /// Fallback tree prototype
    /// </summary>
    [DataField]
    public string TreePrototype = string.Empty;

    /// <summary>
    /// Tree visual variants
    /// </summary>
    [DataField]
    public List<string> TreePrototypes = new();

    /// <summary>
    /// Growth delay
    /// </summary>
    [DataField]
    public float SpawnDelay = 8f;
}
