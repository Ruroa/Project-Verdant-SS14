using System;
using Content.Client.Mapping;
using Content.Shared.Station.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers.Implementations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Maps;

public sealed class PlanetaryGridTargetSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    public bool IgnoreMapRootGrid { get; set; }
    public event Action<bool>? IgnoreMapRootGridChanged;

    public override void Initialize()
    {
        base.Initialize();
        var tileSpawning = _ui.GetUIController<TileSpawningUIController>();
        tileSpawning.TargetLayer1PlacementOption = nameof(AlignPVStationGrid);
        tileSpawning.SetTargetLayer1Only(IgnoreMapRootGrid, notify: false);
        tileSpawning.TargetLayer1OnlyChanged += SetIgnoreMapRootGrid;
    }

    public override void Shutdown()
    {
        var tileSpawning = _ui.GetUIController<TileSpawningUIController>();
        tileSpawning.TargetLayer1OnlyChanged -= SetIgnoreMapRootGrid;
        tileSpawning.TargetLayer1PlacementOption = null;
        base.Shutdown();
    }

    public void SetIgnoreMapRootGrid(bool ignoreRoot)
    {
        if (IgnoreMapRootGrid == ignoreRoot)
            return;

        IgnoreMapRootGrid = ignoreRoot;
        _ui.GetUIController<TileSpawningUIController>().SetTargetLayer1Only(ignoreRoot, notify: false);
        IgnoreMapRootGridChanged?.Invoke(ignoreRoot);
    }

    public bool TryGetStationGridCoordinates(EntityCoordinates coordinates, out EntityCoordinates stationCoordinates)
    {
        stationCoordinates = default;
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        var mapId = _transform.GetMapId(coordinates);
        var mapUid = _map.GetMapOrInvalid(mapId);
        EntityCoordinates? fallback = null;

        foreach (var grid in _map.GetAllGrids(mapId))
        {
            if (grid.Owner == mapUid)
                continue;

            var localPosition = _map.WorldToLocal(grid.Owner, grid.Comp, mapCoordinates.Position);
            // Include empty grid cells.
            if (!grid.Comp.LocalAABB.Contains(localPosition))
                continue;

            var candidate = new EntityCoordinates(grid.Owner, localPosition);
            if (HasComp<StationMemberComponent>(grid.Owner))
            {
                stationCoordinates = candidate;
                return true;
            }

            fallback ??= candidate;
        }

        if (fallback is not { } fallbackCoordinates)
            return false;

        stationCoordinates = fallbackCoordinates;
        return true;
    }

    public bool TryGetStationGridTile(EntityCoordinates coordinates, out EntityUid gridUid, out TileRef tile)
    {
        gridUid = EntityUid.Invalid;
        tile = default;

        if (!TryGetStationGridCoordinates(coordinates, out var stationCoordinates) ||
            !TryComp<MapGridComponent>(stationCoordinates.EntityId, out var grid))
            return false;

        gridUid = stationCoordinates.EntityId;
        tile = _map.GetTileRef(gridUid, grid, stationCoordinates);
        return true;
    }
}
