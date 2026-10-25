using Content.Shared.Atmos;
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._PV.Terraforming;

[RegisterComponent, NetworkedComponent]
public sealed partial class TerraformerComponent : Component
{
    [DataField]
    public float Radius = 5f;

    [DataField]
    public float Fuel = 0f;

    [DataField]
    public float MaxFuel = 100f;

    [DataField]
    public float FuelPerSecond = 1f;

    [DataField]
    public float FuelPerBiomass = 10f;

    [DataField]
    public float TileConvertCooldown = 2f;

    /// <summary>
    /// Tile research points
    /// </summary>
    [DataField]
    public int SciencePointsPerTile = 5;

    [DataField]
    public int TilesTerraformed = 0;

    [DataField]
    public float Accumulator = 0f;

    [DataField]
    public bool Active = true;

    [DataField]
    public ProtoId<StackPrototype> BiomassStack = "Biomass";

    [DataField]
    public List<string> SourceTiles = new();

    /// <summary>
    /// Transition tile
    /// </summary>
    [DataField]
    public string IntermediateTile = "FloorDirt";

    [DataField]
    public string TargetTile = "FloorGrass";

    [DataField]
    public float AtmosCooldown = 1f;

    [DataField]
    public float AtmosAccumulator = 0f;

    [DataField]
    public float TargetPressure = Atmospherics.OneAtmosphere;

    [DataField]
    public float MaxPressure = 110f;

    [DataField]
    public float GasMolesPerTile = 0.25f;

    [DataField]
    public bool ScrubGases = true;

    [DataField]
    public float ScrubCooldown = 1f;

    [DataField]
    public float ScrubAccumulator = 0f;

    [DataField]
    public float ScrubMolesPerTile = 1f;

    [DataField]
    public float TargetScrubMoles = 0f;

    [DataField]
    public List<Gas> ScrubbedGases = new()
    {
        Gas.CarbonDioxide
    };

    [DataField]
    public bool CreateBarriers = true;

    [DataField]
    public string BarrierPrototype = "TerraformerAtmosBarrier";

    /// <summary>
    /// Barrier distance
    /// Nonpositive uses radius
    /// </summary>
    [DataField]
    public float BarrierRadius = 0f;

    /// <summary>
    /// Barrier repair interval
    /// </summary>
    [DataField]
    public float BarrierRefreshCooldown = 6f;

    [DataField]
    public float BarrierRefreshAccumulator = 0f;

    /// <summary>
    /// Network repair trigger
    /// </summary>
    [DataField]
    public bool ForceBarrierRefresh = false;

    /// <summary>
    /// Shared barrier list
    /// </summary>
    public List<EntityUid> SpawnedBarriers = new();

    /// <summary>
    /// Legacy tree spawning
    /// </summary>
    [DataField]
    public bool SpawnTrees = false;

    /// <summary>
    /// Legacy tree limit
    /// </summary>
    [DataField]
    public int MaxSpawnedTrees = 4;

    [DataField]
    public int SpawnedTrees = 0;

    [DataField]
    public float TreeSpawnAccumulator = 0f;

    [DataField]
    public float TreeSpawnCooldown = 20f;

    [DataField]
    public float TreeSpawnChance = 0.25f;

    /// <summary>
    /// Legacy tree prototype
    /// </summary>
    [DataField]
    public string TreePrototype = "FloraTree";

    /// <summary>
    /// Valid tree tiles
    /// </summary>
    [DataField]
    public List<string> TreeSpawnTiles = new()
    {
        "FloorGrass"
    };
}
