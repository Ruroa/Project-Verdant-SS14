using System.Numerics;
using Content.Client.Maps;
using Robust.Client.Placement;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Mapping;

public sealed class AlignPVStationGrid : PlacementMode
{
    [Dependency] private IEntityManager _entities = default!;

    private readonly PlanetaryGridTargetSystem _targeting;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;

    public AlignPVStationGrid(PlacementManager manager) : base(manager)
    {
        IoCManager.InjectDependencies(this);
        _targeting = _entities.System<PlanetaryGridTargetSystem>();
        _map = _entities.System<SharedMapSystem>();
        _transform = _entities.System<SharedTransformSystem>();
    }

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        var rawCoordinates = ScreenToCursorGrid(mouseScreen);
        if (!_targeting.TryGetStationGridCoordinates(rawCoordinates, out var stationCoordinates))
        {
            MouseCoords = _transform.ToMapCoordinates(rawCoordinates);
            GridDistancing = 1f;
            return;
        }

        MouseCoords = stationCoordinates.AlignWithClosestGridTile(2f, _entities);
        var gridUid = _transform.GetGrid(MouseCoords);
        if (!_entities.TryGetComponent<MapGridComponent>(gridUid, out var grid))
            return;

        CurrentTile = _map.GetTileRef(gridUid!.Value, grid, MouseCoords);
        GridDistancing = grid.TileSize;

        if (pManager.CurrentPermission!.IsTile)
        {
            MouseCoords = new EntityCoordinates(MouseCoords.EntityId,
                new Vector2(CurrentTile.X + grid.TileSize / 2, CurrentTile.Y + grid.TileSize / 2));
        }
        else
        {
            MouseCoords = new EntityCoordinates(MouseCoords.EntityId,
                new Vector2(CurrentTile.X + grid.TileSize / 2 + pManager.PlacementOffset.X,
                    CurrentTile.Y + grid.TileSize / 2 + pManager.PlacementOffset.Y));
        }
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        return !pManager.CurrentPermission!.IsTile || _targeting.TryGetStationGridCoordinates(position, out _);
    }
}
