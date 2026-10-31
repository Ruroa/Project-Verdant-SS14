using Robust.Shared.Maths;

namespace Content.Server._PV.PlanetaryLayer;

[RegisterComponent]
public sealed partial class PlanetaryWallCollisionComponent : Component
{
    public EntityUid? SurfaceGrid;
    public Vector2i? TileIndices;
}

[RegisterComponent]
public sealed partial class PlanetaryWallCollisionGridComponent : Component
{
}
