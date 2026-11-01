using System.Collections.Generic;
using Content.Shared._PV.Parallax;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Map;

namespace Content.Client._PV.Parallax;

/// <summary>
/// Draws planet river sprites immediately after the root planet grid. Layered
/// station grids are rendered afterward, so their tiles cover the water.
/// </summary>
public sealed class PlanetRiverWaterOverlay : GridOverlay
{
    private readonly IEntityManager _entities;
    private readonly EntityLookupSystem _lookup;
    private readonly SpriteSystem _sprites;
    private readonly SharedTransformSystem _transform;
    private readonly HashSet<EntityUid> _visibleEntities = new();

    public PlanetRiverWaterOverlay(IEntityManager entities)
    {
        _entities = entities;
        _lookup = entities.System<EntityLookupSystem>();
        _sprites = entities.System<SpriteSystem>();
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace || Grid.Owner != args.MapUid)
            return;

        _visibleEntities.Clear();
        _lookup.GetEntitiesIntersecting(Grid.Owner, args.WorldAABB, _visibleEntities);
        foreach (var uid in _visibleEntities)
        {
            if (!_entities.HasComponent<PlanetRiverWaterComponent>(uid) ||
                !_entities.TryGetComponent(uid, out SpriteComponent? sprite) ||
                !_entities.TryGetComponent(uid, out TransformComponent? xform))
            {
                continue;
            }

            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
            _sprites.RenderSprite((uid, sprite), args.WorldHandle, args.Viewport.Eye?.Rotation ?? Angle.Zero,
                worldRot, worldPos);
            RequiresFlush = true;
        }
    }
}
