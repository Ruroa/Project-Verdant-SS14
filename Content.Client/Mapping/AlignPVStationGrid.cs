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
    private bool _upperGridSelected;

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
            _upperGridSelected = false;
            MouseCoords = _transform.ToCoordinates(_transform.ToMapCoordinates(rawCoordinates));
            GridDistancing = 1f;
            return;
        }

        _upperGridSelected = true;
        var gridUid = stationCoordinates.EntityId;
        if (!_entities.TryGetComponent<MapGridComponent>(gridUid, out var grid))
            return;

        var tileIndices = _map.CoordinatesToTile(gridUid, grid, stationCoordinates);
        CurrentTile = _map.GetTileRef(gridUid, grid, tileIndices);
        GridDistancing = grid.TileSize;
        MouseCoords = _map.GridTileToLocal(gridUid, grid, tileIndices);

        if (!pManager.CurrentPermission!.IsTile)
        {
            MouseCoords = new EntityCoordinates(MouseCoords.EntityId, MouseCoords.Position + pManager.PlacementOffset);
        }
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        return _upperGridSelected &&
               (!pManager.CurrentPermission!.IsTile || _targeting.TryGetStationGridCoordinates(position, out _));
    }
}
