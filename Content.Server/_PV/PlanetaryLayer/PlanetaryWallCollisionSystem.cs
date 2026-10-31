using System.Numerics;
using Content.Server.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._PV.PlanetaryLayer;

public sealed partial class PlanetaryWallCollisionSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlanetaryWallCollisionComponent, ComponentStartup>(OnWallStartup);
        SubscribeLocalEvent<PlanetaryWallCollisionComponent, MapInitEvent>(OnWallMapInit);
        SubscribeLocalEvent<PlanetaryWallCollisionComponent, AnchorStateChangedEvent>(OnWallAnchorChanged);
        SubscribeLocalEvent<PlanetaryWallCollisionComponent, ComponentShutdown>(OnWallShutdown);
    }

    private void OnWallStartup(EntityUid uid, PlanetaryWallCollisionComponent component, ComponentStartup args)
    {
        SyncProxy(uid, component);
    }

    private void OnWallMapInit(EntityUid uid, PlanetaryWallCollisionComponent component, MapInitEvent args)
    {
        SyncProxy(uid, component);
    }

    private void OnWallAnchorChanged(EntityUid uid, PlanetaryWallCollisionComponent component, ref AnchorStateChangedEvent args)
    {
        SyncProxy(uid, component);
    }

    private void OnWallShutdown(EntityUid uid, PlanetaryWallCollisionComponent component, ComponentShutdown args)
    {
        RemoveProxy(component);
    }

    public void RefreshGrid(EntityUid uid)
    {
        var query = EntityQueryEnumerator<PlanetaryWallCollisionComponent, TransformComponent>();
        while (query.MoveNext(out var wall, out var wallComponent, out var xform))
        {
            if (xform.GridUid == uid)
                SyncProxy(wall, wallComponent);
        }
    }

    private void SyncProxy(EntityUid uid, PlanetaryWallCollisionComponent component)
    {
        if (!TryComp(uid, out TransformComponent? xform) ||
            !xform.Anchored ||
            xform.GridUid is not { } gridUid ||
            !HasComp<PlanetarySurfaceComponent>(gridUid) ||
            xform.MapUid is not { } mapUid)
        {
            RemoveProxy(component);
            return;
        }

        var mapCoordinates = _transform.ToMapCoordinates(new EntityCoordinates(uid, Vector2.Zero));
        if (component.Proxy is { } existing && Exists(existing))
        {
            _transform.SetCoordinates(existing, new EntityCoordinates(mapUid, mapCoordinates.Position));
            return;
        }

        component.Proxy = Spawn("PVPlanetaryWallCollisionProxy", mapCoordinates);
    }

    private void RemoveProxy(PlanetaryWallCollisionComponent component)
    {
        if (component.Proxy is not { } proxy)
            return;

        if (Exists(proxy))
            QueueDel(proxy);

        component.Proxy = null;
    }
}
