using Robust.Shared.Utility;

namespace Content.Server._PV.PlanetaryLayer;

[RegisterComponent]
public sealed partial class PlanetarySurfaceWreckSpawnComponent : Component
{
    [DataField(required: true)] public List<ResPath> Paths = new();
    [DataField] public int MinCount = 1;
    [DataField] public int MaxCount = 2;
    [DataField] public float MinimumDistance = 140f;
    [DataField] public float MaximumDistance = 260f;

    [ViewVariables] public bool Spawned;
}
