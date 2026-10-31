using Content.Shared.Maps;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._PV.PlanetaryLayer;

public sealed partial class PlanetaryWallCollisionSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    private readonly Dictionary<EntityUid, EntityUid> _collisionGrids = new();
    private readonly Dictionary<(EntityUid Surface, Vector2i Tile), int> _wallCounts = new();
    private readonly HashSet<(EntityUid Surface, Vector2i Tile)> _dirtyTiles = new();
    private ContentTileDefinition _spaceTile = default!;

    public override void Initialize()
    {
        base.Initialize();

        _spaceTile = (ContentTileDefinition) _tileDefinitions["Space"];

        SubscribeLocalEvent<PlanetaryWallCollisionComponent, ComponentStartup>(OnWallStartup);
        SubscribeLocalEvent<PlanetaryWallCollisionComponent, MapInitEvent>(OnWallMapInit);
        SubscribeLocalEvent<PlanetaryWallCollisionComponent, AnchorStateChangedEvent>(OnWallAnchorChanged);
        SubscribeLocalEvent<PlanetaryWallCollisionComponent, ComponentShutdown>(OnWallShutdown);
        SubscribeLocalEvent<PlanetaryWallCollisionGridComponent, GridFixtureChangeEvent>(OnCollisionGridFixtureChange);
        SubscribeLocalEvent<PlanetarySurfaceComponent, ComponentShutdown>(OnSurfaceShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_dirtyTiles.Count == 0)
            return;

        var dirty = _dirtyTiles.ToArray();
        _dirtyTiles.Clear();
        foreach (var group in dirty.GroupBy(entry => entry.Surface))
        {
            if (!_collisionGrids.TryGetValue(group.Key, out var collisionGridUid) ||
                !TryComp(collisionGridUid, out MapGridComponent? collisionGrid))
                continue;

            var tiles = new List<(Vector2i GridIndices, Tile Tile)>();
            foreach (var entry in group)
            {
                var tile = _wallCounts.ContainsKey(entry)
                    ? new Tile(_spaceTile.TileId)
                    : Tile.Empty;
                tiles.Add((entry.Tile, tile));
            }

            _maps.SetTiles((collisionGridUid, collisionGrid), tiles);
        }
    }

    private void OnWallStartup(EntityUid uid, PlanetaryWallCollisionComponent component, ComponentStartup args)
    {
        SyncWall(uid, component);
    }

    private void OnWallMapInit(EntityUid uid, PlanetaryWallCollisionComponent component, MapInitEvent args)
    {
        SyncWall(uid, component);
    }

    private void OnWallAnchorChanged(EntityUid uid, PlanetaryWallCollisionComponent component, ref AnchorStateChangedEvent args)
    {
        SyncWall(uid, component);
    }

    private void OnWallShutdown(EntityUid uid, PlanetaryWallCollisionComponent component, ComponentShutdown args)
    {
        RemoveWallTile(component);
    }

    public void RefreshGrid(EntityUid uid)
    {
        var collisionGrid = EnsureCollisionGrid(uid);
        if (collisionGrid == EntityUid.Invalid)
            return;

        var query = EntityQueryEnumerator<PlanetaryWallCollisionComponent, TransformComponent>();
        while (query.MoveNext(out var wall, out var wallComponent, out var xform))
        {
            if (xform.GridUid == uid)
                SyncWall(wall, wallComponent);
        }
    }

    private void OnCollisionGridFixtureChange(EntityUid uid, PlanetaryWallCollisionGridComponent component, GridFixtureChangeEvent args)
    {
        ApplyCollisionGridFilters(uid, args.NewFixtures);
    }

    private void OnSurfaceShutdown(EntityUid uid, PlanetarySurfaceComponent component, ComponentShutdown args)
    {
        if (_collisionGrids.Remove(uid, out var collisionGrid) && Exists(collisionGrid))
            QueueDel(collisionGrid);

        var keys = new List<(EntityUid Surface, Vector2i Tile)>();
        foreach (var key in _wallCounts.Keys)
        {
            if (key.Surface == uid)
                keys.Add(key);
        }

        foreach (var key in keys)
        {
            _wallCounts.Remove(key);
            _dirtyTiles.Remove(key);
        }
    }

    private void SyncWall(EntityUid uid, PlanetaryWallCollisionComponent component)
    {
        if (!TryComp(uid, out TransformComponent? xform) ||
            !xform.Anchored ||
            xform.GridUid is not { } gridUid ||
            !HasComp<PlanetarySurfaceComponent>(gridUid) ||
            !TryComp(gridUid, out MapGridComponent? surfaceGrid))
        {
            RemoveWallTile(component);
            return;
        }

        var collisionGridUid = EnsureCollisionGrid(gridUid);
        if (collisionGridUid == EntityUid.Invalid || !TryComp(collisionGridUid, out MapGridComponent? collisionGrid))
            return;

        var tile = _maps.TileIndicesFor(gridUid, surfaceGrid, xform.Coordinates);
        if (component.SurfaceGrid == gridUid && component.TileIndices == tile)
            return;

        RemoveWallTile(component);

        var key = (gridUid, tile);
        _wallCounts.TryGetValue(key, out var count);
        _wallCounts[key] = count + 1;
        component.SurfaceGrid = gridUid;
        component.TileIndices = tile;

        if (count == 0)
            _dirtyTiles.Add(key);
    }

    private EntityUid EnsureCollisionGrid(EntityUid surfaceUid)
    {
        if (_collisionGrids.TryGetValue(surfaceUid, out var existing) && Exists(existing))
            return existing;

        if (!TryComp(surfaceUid, out TransformComponent? surfaceXform) || surfaceXform.MapUid is not { } mapUid)
            return EntityUid.Invalid;

        var query = EntityQueryEnumerator<PlanetaryWallCollisionGridComponent, TransformComponent>();
        while (query.MoveNext(out var candidate, out _, out var candidateXform))
        {
            if (candidateXform.MapID != surfaceXform.MapID ||
                candidateXform.Coordinates != surfaceXform.Coordinates ||
                candidateXform.LocalRotation != surfaceXform.LocalRotation)
                continue;

            _collisionGrids[surfaceUid] = candidate;
            return candidate;
        }

        var grid = _maps.CreateGridEntity(surfaceXform.MapID);
        var collisionGridUid = grid.Owner;
        grid.Comp.CanSplit = false;
        _transform.SetCoordinates(collisionGridUid, new EntityCoordinates(mapUid, surfaceXform.Coordinates.Position));
        _transform.SetLocalRotation(collisionGridUid, surfaceXform.LocalRotation);
        EnsureComp<PlanetaryWallCollisionGridComponent>(collisionGridUid);
        RemComp<ImplicitRoofComponent>(collisionGridUid);

        if (TryComp(collisionGridUid, out ShuttleComponent? shuttle))
            shuttle.Enabled = false;

        if (TryComp(collisionGridUid, out PhysicsComponent? physics))
            _physics.SetBodyType(collisionGridUid, BodyType.Static, body: physics);

        _collisionGrids[surfaceUid] = collisionGridUid;
        return collisionGridUid;
    }

    private void ApplyCollisionGridFilters(EntityUid uid, Dictionary<string, Fixture> fixtures)
    {
        if (!TryComp(uid, out FixturesComponent? manager))
            return;

        foreach (var (fixtureId, fixture) in fixtures)
        {
            _physics.SetCollisionLayer(uid, fixtureId, fixture, (int) CollisionGroup.PlanetaryWall, manager);
            _physics.SetCollisionMask(uid, fixtureId, fixture, (int) CollisionGroup.PlanetaryShuttle, manager);
        }
    }

    private void RemoveWallTile(PlanetaryWallCollisionComponent component)
    {
        if (component.SurfaceGrid is not { } surfaceUid || component.TileIndices is not { } tile)
            return;

        var key = (surfaceUid, tile);
        if (!_wallCounts.TryGetValue(key, out var count))
        {
            component.SurfaceGrid = null;
            component.TileIndices = null;
            return;
        }

        if (count <= 1)
        {
            _wallCounts.Remove(key);
            _dirtyTiles.Add(key);
        }
        else
        {
            _wallCounts[key] = count - 1;
        }

        component.SurfaceGrid = null;
        component.TileIndices = null;
    }
}
