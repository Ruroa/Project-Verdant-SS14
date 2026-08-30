namespace Content.Server._PV.Weather.Components;

/// <summary>
/// Stores mapper-selected roof overrides so automatic roof calculation does not
/// erase courtyards or other deliberately exposed/covered tiles.
/// </summary>
[RegisterComponent]
public sealed partial class RoofOverrideComponent : Component
{
    [DataField]
    public HashSet<Vector2i> ForceRoof = new();

    [DataField]
    public HashSet<Vector2i> ForceNoRoof = new();
}
