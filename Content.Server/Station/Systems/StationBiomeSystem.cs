using System.Numerics;
using Content.Server.Parallax;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Station.Systems;

public sealed partial class StationBiomeSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationBiomeComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<StationBiomeComponent> map, ref StationPostInitEvent args)
    {
        var station = _station.GetLargestGrid(map.Owner);
        if (station == null)
            return;

        MoveStationToOrigin(map.Owner, station.Value);

        var mapId = Transform(station.Value).MapID;
        var mapUid = _map.GetMapOrInvalid(mapId);

        _biome.EnsurePlanetarySurface(mapUid, _proto.Index(map.Comp.Biome), map.Comp.Seed,
            map.Comp.MapLightColor, map.Comp.OreMarkers);
    }

    private void MoveStationToOrigin(EntityUid stationUid, EntityUid mainGrid)
    {
        var mainCoordinates = _transform.ToMapCoordinates(new EntityCoordinates(mainGrid, Vector2.Zero));
        var offset = -mainCoordinates.Position;

        if (offset.LengthSquared() <= 0.0001f)
            return;

        if (!TryComp<StationDataComponent>(stationUid, out var stationData) || stationData == null)
        {
            _transform.SetMapCoordinates(mainGrid, new MapCoordinates(Vector2.Zero, mainCoordinates.MapId));
            return;
        }

        foreach (var grid in stationData.Grids)
        {
            if (!TryComp<MapGridComponent>(grid, out _) ||
                !TryComp<TransformComponent>(grid, out var gridXform) ||
                gridXform.MapID != mainCoordinates.MapId)
                continue;

            var coordinates = _transform.ToMapCoordinates(new EntityCoordinates(grid, Vector2.Zero));
            _transform.SetMapCoordinates(grid, new MapCoordinates(coordinates.Position + offset, coordinates.MapId));
        }
    }
}
