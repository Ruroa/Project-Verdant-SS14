namespace Content.Server._PV.Weather.Components;

/// <summary>
/// Mapper roof override
/// </summary>
[RegisterComponent]
public sealed partial class RoofOverrideComponent : Component
{
    [DataField]
    public HashSet<Vector2i> ForceRoof = new();

    [DataField]
    public HashSet<Vector2i> ForceNoRoof = new();
}
