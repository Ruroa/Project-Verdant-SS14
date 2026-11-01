using Content.Server.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server._PV.OrbitalAccessPoint;

/// <summary>
/// Loads a station's separate orbital access map and nearby grids during station setup.
/// </summary>
[RegisterComponent]
public sealed partial class OrbitalAccessPointComponent : Component
{
    [DataField(required: true)]
    public ResPath MapPath = default!;

    [DataField(required: true)]
    public Dictionary<string, IGridSpawnGroup> SpawnGroups = new();

    /// <summary>
    /// Runtime grid used as the salvage expedition's return destination.
    /// </summary>
    [ViewVariables]
    public EntityUid? AccessGrid;
}
