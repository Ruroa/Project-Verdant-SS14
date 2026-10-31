using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Tiles;

public sealed partial class FloorTileSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stackSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly Vector2 CheckRange = new(1f, 1f);
    private static readonly ProtoId<ContentTileDefinition> LatticeTile = "Lattice";
    private static readonly Vector2i[] LatticeSupportOffsets =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    /// <summary>
    ///     A recycled hashset used to check for walls when trying to place tiles on turfs.
    /// </summary>
    private readonly HashSet<EntityUid> _turfCheck = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FloorTileComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, FloorTileComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp<StackComponent>(uid, out var stack))
            return;

        if (component.Outputs == null)
            return;

        var latticePlacement = component.Outputs.Contains(LatticeTile);
        var location = args.ClickLocation;
        if (latticePlacement && !TryGetLayerOneCoordinates(location, out location))
        {
            if (_netManager.IsClient && _timing.IsFirstTimePredicted)
                _popup.PopupEntity(Loc.GetString("invalid-floor-placement"), args.User);

            return;
        }

        location = location.AlignWithClosestGridTile();
        if (latticePlacement && !HasLayerOneSupport(location))
        {
            if (_netManager.IsClient && _timing.IsFirstTimePredicted)
                _popup.PopupEntity(Loc.GetString("invalid-floor-placement"), args.User);

            return;
        }

        var locationMap = _transform.ToMapCoordinates(location);
        if (locationMap.MapId == MapId.Nullspace)
            return;

        var physicQuery = GetEntityQuery<PhysicsComponent>();
        var transformQuery = GetEntityQuery<TransformComponent>();

        var map = _transform.ToMapCoordinates(location);

        // Disallow placement close to grids.
        // FTLing close is okay but this makes alignment too finnicky.
        // While you may already have a tile close you want to replace when we get half-tiles that may also be finnicky
        // so we're just gon with this for now.
        const bool inRange = true;
        var mapRoot = _map.GetMapOrInvalid(map.MapId);
        var state = (inRange, location.EntityId, mapRoot, latticePlacement);
        _map.FindGridsIntersecting(map.MapId, new Box2(map.Position - CheckRange, map.Position + CheckRange), ref state,
            static (EntityUid entityUid, MapGridComponent grid, ref (bool weh, EntityUid EntityId, EntityUid MapRoot, bool IgnoreMapRoot) tuple) =>
            {
                if (tuple.IgnoreMapRoot && entityUid == tuple.MapRoot)
                    return true;

                if (tuple.EntityId == entityUid)
                    return true;

                tuple.weh = false;
                return false;
            });

        if (!state.inRange)
        {
            if (_netManager.IsClient && _timing.IsFirstTimePredicted)
                _popup.PopupEntity(Loc.GetString("invalid-floor-placement"), args.User);

            return;
        }

        var userPos = _transform.ToMapCoordinates(transformQuery.GetComponent(args.User).Coordinates).Position;
        var dir = userPos - map.Position;
        var canAccessCenter = false;
        if (dir.LengthSquared() > 0.01)
        {
            var ray = new CollisionRay(map.Position, dir.Normalized(), (int) CollisionGroup.Impassable);
            var results = _physics.IntersectRay(locationMap.MapId, ray, dir.Length(), returnOnFirstHit: true);
            canAccessCenter = !results.Any();
        }

        // if user can access tile center then they can place floor
        // otherwise check it isn't blocked by a wall
        if (!canAccessCenter && _turf.TryGetTileRef(location, out var tileRef))
        {
            _turfCheck.Clear();
            _lookup.GetEntitiesInTile(tileRef.Value, _turfCheck);
            foreach (var ent in _turfCheck)
            {
                if (physicQuery.TryGetComponent(ent, out var phys) &&
                    phys.BodyType == BodyType.Static &&
                    phys.Hard &&
                    (phys.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
                {
                    return;
                }
            }
        }
        TryComp<MapGridComponent>(location.EntityId, out var mapGrid);

        foreach (var currentTile in component.Outputs)
        {
            var currentTileDefinition = (ContentTileDefinition) _tileDefinitionManager[currentTile];

            if (mapGrid != null)
            {
                var gridUid = location.EntityId;
                var tile = _map.GetTileRef(gridUid, mapGrid, location);

                if (!CanPlaceTile(gridUid, mapGrid, tile.GridIndices, out var reason))
                {
                    _popup.PopupClient(reason, args.User, args.User);
                    return;
                }

                var baseTurf = (ContentTileDefinition) _tileDefinitionManager[tile.Tile.TypeId];

                if (CanPlaceOn(currentTileDefinition, baseTurf.ID))
                {
                    if (!_stackSystem.TryUse((uid, stack), 1))
                        continue;

                    PlaceAt(args.User, gridUid, mapGrid, location, currentTileDefinition.TileId, component.PlaceTileSound);
                    args.Handled = true;
                    return;
                }
            }
            else if (HasBaseTurf(currentTileDefinition, new ProtoId<ContentTileDefinition>(ContentTileDefinition.SpaceID)))
            {
                if (!_stackSystem.TryUse((uid, stack), 1))
                    continue;

                args.Handled = true;
                if (_netManager.IsClient)
                    return;

                var grid = _map.CreateGridEntity(locationMap.MapId);
                var gridXform = Transform(grid);
                _transform.SetWorldPosition((grid, gridXform), locationMap.Position);
                location = new EntityCoordinates(grid, Vector2.Zero);
                PlaceAt(args.User, grid, grid.Comp, location, _tileDefinitionManager[component.Outputs[0]].TileId, component.PlaceTileSound, grid.Comp.TileSize / 2f);
                return;
            }
        }
    }

    public bool HasBaseTurf(ContentTileDefinition tileDef, ProtoId<ContentTileDefinition> baseTurf)
    {
        return tileDef.BaseTurf == baseTurf;
    }

    private bool TryGetLayerOneCoordinates(EntityCoordinates coordinates, out EntityCoordinates layerOneCoordinates)
    {
        layerOneCoordinates = default;
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        var mapUid = _map.GetMapOrInvalid(mapCoordinates.MapId);
        EntityCoordinates? fallback = null;

        foreach (var grid in _map.GetAllGrids(mapCoordinates.MapId))
        {
            if (grid.Owner == mapUid)
                continue;

            var localPosition = _map.WorldToLocal(grid.Owner, grid.Comp, mapCoordinates.Position);
            var bounds = grid.Comp.LocalAABB;
            var padding = grid.Comp.TileSize;
            if (localPosition.X < bounds.Left - padding || localPosition.X > bounds.Right + padding ||
                localPosition.Y < bounds.Bottom - padding || localPosition.Y > bounds.Top + padding)
                continue;

            var candidate = new EntityCoordinates(grid.Owner, localPosition);
            if (HasComp<StationMemberComponent>(grid.Owner))
            {
                layerOneCoordinates = candidate;
                return true;
            }

            fallback ??= candidate;
        }

        if (fallback is not { } fallbackCoordinates)
            return false;

        layerOneCoordinates = fallbackCoordinates;
        return true;
    }

    private bool HasLayerOneSupport(EntityCoordinates coordinates)
    {
        if (!TryComp<MapGridComponent>(coordinates.EntityId, out var grid))
            return false;

        var gridIndices = _map.TileIndicesFor(coordinates.EntityId, grid, coordinates);
        foreach (var offset in LatticeSupportOffsets)
        {
            if (_map.TryGetTileRef(coordinates.EntityId, grid, gridIndices + offset, out var tile) &&
                !tile.Tile.IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanPlaceOn(ContentTileDefinition tileDef, ProtoId<ContentTileDefinition> currentTurfId)
    {
        //Check exact BaseTurf match
        if (tileDef.BaseTurf == currentTurfId)
            return true;

        // Check whitelist match
        if (tileDef.BaseWhitelist.Count > 0 && tileDef.BaseWhitelist.Contains(currentTurfId))
            return true;

        return false;
    }

    private void PlaceAt(EntityUid user, EntityUid gridUid, MapGridComponent mapGrid, EntityCoordinates location,
        ushort tileId, SoundSpecifier placeSound, float offset = 0)
    {
        _adminLogger.Add(LogType.Tile, LogImpact.Low, $"{ToPrettyString(user):actor} placed tile {_tileDefinitionManager[tileId].Name} at {ToPrettyString(gridUid)} {location}");

        var tileDef = (ContentTileDefinition) _tileDefinitionManager[tileId];
        var random = new System.Random((int)_timing.CurTick.Value);
        var variant = _tile.PickVariant(tileDef, random);

        var tileRef = _map.GetTileRef(gridUid, mapGrid, location.Offset(new Vector2(offset, offset)));
        _tile.ReplaceTile(tileRef, tileDef, gridUid, mapGrid, variant: variant);

        _audio.PlayPredicted(placeSound, location, user);
    }

    public bool CanPlaceTile(EntityUid gridUid, MapGridComponent component, Vector2i gridIndices, [NotNullWhen(false)] out string? reason)
    {
        var ev = new FloorTileAttemptEvent(gridIndices);
        RaiseLocalEvent(gridUid, ref ev);

        if (ev.Cancelled)
        {
            reason = Loc.GetString("invalid-floor-placement");
            return false;
        }

        reason = null;
        return true;
    }
}
