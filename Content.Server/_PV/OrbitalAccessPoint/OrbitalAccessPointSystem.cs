using Content.Server.Cargo.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;

namespace Content.Server._PV.OrbitalAccessPoint;

public sealed class OrbitalAccessPointSystem : EntitySystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Finish planet-side grid spawns before loading the separate orbital map.
        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit, after: [typeof(ShuttleSystem)]);
    }

    private void OnStationPostInit(ref StationPostInitEvent ev)
    {
        var station = ev.Station.Owner;
        if (!TryComp(station, out OrbitalAccessPointComponent? accessPoint) ||
            accessPoint.AccessGrid != null)
        {
            return;
        }

        var mapUid = _map.CreateMap(out var mapId, runMapInit: false);
        var options = DeserializationOptions.Default with { InitializeMaps = false };

        if (!_mapLoader.TryLoadGrid(mapId, accessPoint.MapPath, out var accessGrid, options))
        {
            Log.Error($"Failed to load orbital access map from {accessPoint.MapPath} for {ToPrettyString(station)}.");
            _map.DeleteMap(mapId);
            return;
        }

        accessPoint.AccessGrid = accessGrid.Value.Owner;

        // Keep the orbital point station-owned so its consoles and shuttles belong to
        // the same station, while its separate MapId keeps its spawns in orbit.
        _station.AddGridToStation(station, accessPoint.AccessGrid.Value);
        // Approved cargo orders search station-owned TradeStation grids and place
        // deliveries on their buy pallets. The OAP is the station's trade station.
        EnsureComp<TradeStationComponent>(accessPoint.AccessGrid.Value);
        _map.InitializeMap(mapUid);

        // Make the separate orbital map selectable through shuttle consoles.
        _shuttle.TryAddFTLDestination(mapId, true, requireDisk: false, beaconsOnly: false, out _);

        // ATS, salvage/mining shuttles, asteroids, and derelicts are placed around
        // this orbital grid rather than around the planetary station grid.
        _shuttle.SpawnGridGroups(station, accessPoint.AccessGrid.Value, accessPoint.SpawnGroups);
    }
}
