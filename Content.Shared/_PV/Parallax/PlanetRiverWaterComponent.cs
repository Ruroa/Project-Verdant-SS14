using Robust.Shared.GameStates;

namespace Content.Shared._PV.Parallax;

/// <summary>
/// Marks procedural planet water whose sprite is drawn between the planet grid
/// and any grids layered above it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlanetRiverWaterComponent : Component;
