using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Client.Maps;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.RCD;

public sealed partial class AlignRCDConstruction : PlacementMode
{
    [Dependency] private IEntityManager _entityManager = default!;
    private readonly SharedMapSystem _mapSystem;
    private readonly HandsSystem _handsSystem;
    private readonly RCDSystem _rcdSystem;
    private readonly SharedTransformSystem _transformSystem;
    private readonly PlanetaryGridTargetSystem _gridTargeting;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IStateManager _stateManager = default!;

    private const float SearchBoxSize = 2f;
    private const float PlaceColorBaseAlpha = 0.5f;

    private EntityCoordinates _unalignedMouseCoords = default;
    private bool _stationGridSelected;

    /// <summary>
    /// This placement mode is not on the engine because it is content specific (i.e., for the RCD)
    /// </summary>
    public AlignRCDConstruction(PlacementManager pMan) : base(pMan)
    {
        IoCManager.InjectDependencies(this);
        _mapSystem = _entityManager.System<SharedMapSystem>();
        _handsSystem = _entityManager.System<HandsSystem>();
        _rcdSystem = _entityManager.System<RCDSystem>();
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _gridTargeting = _entityManager.System<PlanetaryGridTargetSystem>();

        ValidPlaceColor = ValidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
    }

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        _unalignedMouseCoords = ScreenToCursorGrid(mouseScreen);

        var player = _playerManager.LocalSession?.AttachedEntity;
        var heldEntity = player is { } playerUid ? _handsSystem.GetActiveItem(playerUid) : null;
        var ignoreRoot = heldEntity is { } heldUid && _entityManager.TryGetComponent<RCDComponent>(heldUid, out var heldRcd) && heldRcd.IgnorePlanetRootGrid;
        _stationGridSelected = !ignoreRoot;

        if (ignoreRoot)
        {
            if (_gridTargeting.TryGetStationGridCoordinates(_unalignedMouseCoords, out var stationCoordinates))
            {
                _unalignedMouseCoords = stationCoordinates;
                _stationGridSelected = true;
            }
            else
            {
                _stationGridSelected = false;
                MouseCoords = _transformSystem.ToCoordinates(_transformSystem.ToMapCoordinates(_unalignedMouseCoords));
                return;
            }
        }

        MouseCoords = _unalignedMouseCoords.AlignWithClosestGridTile(SearchBoxSize, _entityManager);

        var gridId = _transformSystem.GetGrid(MouseCoords);

        if (!_entityManager.TryGetComponent<MapGridComponent>(gridId, out var mapGrid))
            return;

        CurrentTile = _mapSystem.GetTileRef(gridId.Value, mapGrid, MouseCoords);

        float tileSize = mapGrid.TileSize;
        GridDistancing = tileSize;

        if (pManager.CurrentPermission!.IsTile)
        {
            MouseCoords = new EntityCoordinates(MouseCoords.EntityId, new Vector2(CurrentTile.X + tileSize / 2,
                CurrentTile.Y + tileSize / 2));
        }
        else
        {
            MouseCoords = new EntityCoordinates(MouseCoords.EntityId, new Vector2(CurrentTile.X + tileSize / 2 + pManager.PlacementOffset.X,
                CurrentTile.Y + tileSize / 2 + pManager.PlacementOffset.Y));
        }
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        if (!_stationGridSelected)
            return false;

        var player = _playerManager.LocalSession?.AttachedEntity;

        // If the destination is out of interaction range, set the placer alpha to zero
        if (!_entityManager.TryGetComponent<TransformComponent>(player, out var xform))
            return false;

        if (!_transformSystem.InRange(xform.Coordinates, position, SharedInteractionSystem.InteractionRange))
        {
            InvalidPlaceColor = InvalidPlaceColor.WithAlpha(0);
            return false;
        }

        // Otherwise restore the alpha value
        else
        {
            InvalidPlaceColor = InvalidPlaceColor.WithAlpha(PlaceColorBaseAlpha);
        }

        // Determine if player is carrying an RCD in their active hand
        if (!_handsSystem.TryGetActiveItem(player.Value, out var heldEntity))
            return false;

        if (!_entityManager.TryGetComponent<RCDComponent>(heldEntity, out var rcd))
            return false;

        var gridUid = _transformSystem.GetGrid(position);
        if (!_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var mapGrid))
            return false;
        var tile = _mapSystem.GetTileRef(gridUid.Value, mapGrid, position);
        var posVector = _mapSystem.TileIndicesFor(gridUid.Value, mapGrid, position);

        // Determine if the user is hovering over a target
        var currentState = _stateManager.CurrentState;

        if (currentState is not GameplayStateBase screen)
            return false;

        var target = screen.GetClickedEntity(_transformSystem.ToMapCoordinates(_unalignedMouseCoords));

        // Determine if the RCD operation is valid or not
        if (!_rcdSystem.IsRCDOperationStillValid(heldEntity.Value, rcd, gridUid.Value, mapGrid, tile, posVector, target, player.Value, false))
            return false;

        return true;
    }
}
