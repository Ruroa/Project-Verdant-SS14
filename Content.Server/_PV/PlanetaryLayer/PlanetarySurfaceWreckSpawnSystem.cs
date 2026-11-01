using System.Numerics;
using System.Linq;
using Content.Server._PV.OrbitalAccessPoint;
using Content.Server.Parallax;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._PV.PlanetaryLayer;

public sealed class PlanetarySurfaceWreckSpawnSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private List<Entity<MapGridComponent>> _candidateGrids = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit, after:
        [
            typeof(StationBiomeSystem),
            typeof(ShuttleSystem),
            typeof(OrbitalAccessPointSystem),
        ]);
    }

    private void OnStationPostInit(ref StationPostInitEvent ev)
    {
        var station = ev.Station.Owner;
        if (!TryComp(station, out PlanetarySurfaceWreckSpawnComponent? spawn) ||
            spawn.Spawned || spawn.Paths.Count == 0)
        {
            return;
        }

        var stationGridUid = _station.GetLargestGrid(station);
        if (stationGridUid == null)
            return;

        var mapId = Transform(stationGridUid.Value).MapID;
        var mapUid = _maps.GetMapOrInvalid(mapId);
        if (!HasComp<PlanetarySurfaceComponent>(mapUid) ||
            !HasComp<MapGridComponent>(mapUid))
        {
            return;
        }

        spawn.Spawned = true;

        var stationGrid = Comp<MapGridComponent>(stationGridUid.Value);
        var stationCenter = _transform.ToMapCoordinates(
            new EntityCoordinates(stationGridUid.Value, stationGrid.LocalAABB.Center));
        var stationRadius = MathF.Max(stationGrid.LocalAABB.Width, stationGrid.LocalAABB.Height) / 2f;
        var minimumRadius = stationRadius + spawn.MinimumDistance;
        var maximumRadius = stationRadius + MathF.Max(spawn.MaximumDistance, spawn.MinimumDistance);
        var count = _random.Next(spawn.MinCount, spawn.MaxCount + 1);
        var paths = spawn.Paths.OrderBy(_ => _random.Next()).ToArray();

        for (var i = 0; i < count; i++)
        {
            var path = paths[i % paths.Length];
            if (!_mapLoader.TryLoadGrid(mapId, path, out var wreck) || wreck == null)
            {
                Log.Error($"Failed to load planetary wreck from {path} for {ToPrettyString(station)}.");
                continue;
            }

            var placed = false;
            var wreckUid = wreck.Value.Owner;
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var distance = _random.NextFloat(minimumRadius, maximumRadius);
                var offset = _random.NextAngle().RotateVec(new Vector2(distance, 0f));
                var coordinates = new MapCoordinates(stationCenter.Position + offset, mapId);

                _transform.SetMapCoordinates(wreckUid, coordinates);
                _transform.SetWorldRotation(wreckUid, _transform.GetWorldRotation(mapUid));

                var wreckGrid = Comp<MapGridComponent>(wreckUid);
                var worldBounds = _transform.GetWorldMatrix(wreckUid).TransformBox(wreckGrid.LocalAABB);
                if (OverlapsExistingGrid(mapId, mapUid, wreckUid, worldBounds))
                    continue;

                _biome.ExcludePlanetaryEntities(mapUid, worldBounds);
                placed = true;
                break;
            }

            if (!placed)
            {
                Log.Warning($"Could not find a clear planet-surface location for wreck {path} near {ToPrettyString(station)}.");
                QueueDel(wreckUid);
            }
        }
    }

    private bool OverlapsExistingGrid(MapId mapId, EntityUid planetGrid, EntityUid wreckGrid, Box2 bounds)
    {
        _candidateGrids.Clear();
        _maps.FindGridsIntersecting(mapId, bounds, ref _candidateGrids);

        foreach (var candidate in _candidateGrids)
        {
            if (candidate.Owner == planetGrid ||
                candidate.Owner == wreckGrid ||
                HasComp<PlanetaryWallCollisionGridComponent>(candidate.Owner))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
