using System.Linq;
using System.Numerics;
using Content.Shared.Doors.Components;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Client._PV.DoomView;

internal sealed class DoomViewOverlay : Overlay
{
    private const int Columns = 240;
    private readonly DoomViewSystem _system;
    private readonly IEntityManager _entities;
    private readonly SharedPhysicsSystem _physics;
    private readonly SharedTransformSystem _transforms;
    private readonly EntityLookupSystem _lookup;
    private readonly SpriteSystem _sprites;
    private readonly Texture _wall;
    private readonly DoomModels _models;
    private readonly DoomGround _ground;
    private readonly DoomGeometry _floor = new();
    private readonly DoomGeometry _objects = new();
    private readonly float[] _depths = new float[Columns];
    private readonly Vector2[] _hits = new Vector2[Columns];
    private readonly List<Billboard> _billboards = new();
    private readonly HashSet<EntityUid> _nearby = new();
    private IClydeViewport? _viewport;
    private Vector2i _size;
    private Vector2 _drawSize;
    private EntityUid _player;
    private MapCoordinates _origin;
    private Vector2 _forward;
    private readonly Dictionary<EntityUid, Texture> _wallTextures = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public DoomViewOverlay(DoomViewSystem system, IEntityManager entities)
    {
        _system = system;
        _entities = entities;
        _physics = entities.System<SharedPhysicsSystem>();
        _transforms = entities.System<SharedTransformSystem>();
        _lookup = entities.System<EntityLookupSystem>();
        _sprites = entities.System<SpriteSystem>();
        _models = new DoomModels(entities);
        _ground = new DoomGround(entities);
        _wall = _sprites.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Structures/Walls/solid.rsi"), "full"));
        ZIndex = -100;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.ViewportControl is Content.Client.Viewport.ScalingViewport &&
               _system.TryCamera(out _player, out _origin, out _forward) &&
               args.MapId == _origin.MapId &&
               _entities.TryGetComponent<EyeComponent>(_player, out var eye) &&
               args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _viewport = args.Viewport;
        _size = args.Viewport.Size;
        _wallTextures.Clear();
        _billboards.Clear();
        var handle = args.ScreenHandle;
        var left = args.ViewportBounds.Left;
        var top = args.ViewportBounds.Top;
        var width = args.ViewportBounds.Width;
        var height = args.ViewportBounds.Height;
        if (width <= 0 || height <= 0)
            return;

        args.RenderHandle.SetScissor(args.ViewportBounds);
        _drawSize = new Vector2(width, height);
        var horizon = top + height * 0.5f;
        var focal = DoomProjection.FocalLength(width);
        handle.DrawRect(new UIBox2(left, top, left + width, horizon), new Color(0.07f, 0.09f, 0.12f));
        handle.DrawRect(new UIBox2(left, horizon, left + width, top + height), new Color(0.16f, 0.18f, 0.20f));
        _floor.Begin(_origin.Position, _forward, width, horizon - top);
        _objects.Begin(_origin.Position, _forward, width, horizon - top);
        _ground.Collect(_origin, _forward, _floor);
        CollectBillboards(width, horizon - top, focal);
        foreach (var patch in _floor.Patches.OrderBy(p => p.Layer).ThenByDescending(p => p.Depth))
            DoomGeometry.Draw(patch, args);

        for (var column = 0; column < Columns; column++)
        {
            var direction = DoomProjection.Ray(_forward, (column + 0.5f) / Columns);
            var ray = new CollisionRay(_origin.Position, direction,
                (int) (CollisionGroup.Opaque | CollisionGroup.Impassable | CollisionGroup.InteractImpassable));
            var hit = _physics.IntersectRayWithPredicate(_origin.MapId, ray, DoomProjection.Range,
                IgnoreWall, false).OrderBy(result => result.Distance).FirstOrDefault();
            _depths[column] = float.PositiveInfinity;
            _hits[column] = _origin.Position + direction * DoomProjection.Range;
            if (hit.HitEntity == default)
                continue;

            var depth = MathF.Max(0.05f, Vector2.Dot(hit.HitPos - _origin.Position, _forward));
            _depths[column] = depth;
            _hits[column] = _transforms.GetWorldPosition(hit.HitEntity);
            var wallTop = horizon - focal * (DoomProjection.WallHeight - DoomProjection.EyeHeight) / depth;
            var wallBottom = horizon + focal * DoomProjection.EyeHeight / depth;
            var x1 = left + column * width / (float) Columns;
            var x2 = left + (column + 1) * width / (float) Columns;
            var texture = WallTexture(hit.HitEntity);
            var local = Vector2.Transform(hit.HitPos, _transforms.GetInvWorldMatrix(hit.HitEntity));
            var u = MathF.Abs(local.X) > MathF.Abs(local.Y) ? local.Y + 0.5f : local.X + 0.5f;
            u -= MathF.Floor(u);
            var texX = Math.Min(texture.Width - 1, (int) (u * texture.Width));
            var shade = Math.Clamp(1f - depth / (DoomProjection.Range * 1.2f), 0.18f, 1f);
            var clippedTop = MathF.Max(top, wallTop);
            var clippedBottom = MathF.Min(top + height, wallBottom);
            var texTop = (clippedTop - wallTop) / (wallBottom - wallTop) * texture.Height;
            var texBottom = (clippedBottom - wallTop) / (wallBottom - wallTop) * texture.Height;
            handle.DrawTextureRectRegion(texture, new UIBox2(x1, clippedTop, x2, clippedBottom),
                new UIBox2(texX, texTop, texX + 1, texBottom), new Color(shade, shade, shade));
        }

        var patches = _objects.Patches.OrderByDescending(p => p.Depth).ToArray();
        var patchIndex = 0;
        try
        {
            foreach (var billboard in _billboards.OrderByDescending(b => b.Depth))
            {
                while (patchIndex < patches.Length && patches[patchIndex].Depth > billboard.Depth)
                    DoomGeometry.Draw(patches[patchIndex++], args, _depths);
                var first = Math.Clamp((int) (billboard.Rect.Left / width * Columns), 0, Columns - 1);
                var last = Math.Clamp((int) (billboard.Rect.Right / width * Columns), 0, Columns - 1);
                var runStart = -1;
                for (var column = first; column <= last + 1; column++)
                {
                    var visible = column <= last && billboard.Depth < _depths[column];
                    if (visible && runStart == -1)
                        runStart = column;
                    if (visible || runStart == -1)
                        continue;
                    args.RenderHandle.SetScissor(new UIBox2i(
                        left + (int) (runStart * width / (float) Columns), top,
                        left + (int) MathF.Ceiling(column * width / (float) Columns), top + height));
                    handle.DrawEntity(billboard.Entity,
                        new Vector2(left + billboard.Center.X, top + billboard.Center.Y),
                        new Vector2(billboard.Scale), Angle.Zero,
                        overrideDirection: billboard.Direction);
                    runStart = -1;
                }
            }
            while (patchIndex < patches.Length)
                DoomGeometry.Draw(patches[patchIndex++], args, _depths);
        }
        finally
        {
            args.RenderHandle.SetScissor(args.ViewportBounds);
        }
        var center = new Vector2(left + width * 0.5f, horizon);
        handle.DrawLine(center - new Vector2(4, 0), center + new Vector2(4, 0), Color.White);
        handle.DrawLine(center - new Vector2(0, 4), center + new Vector2(0, 4), Color.White);
    }

    private bool IgnoreWall(EntityUid uid)
    {
        return uid == _player || !_entities.TryGetComponent<TransformComponent>(uid, out var transform) ||
               !transform.Anchored || !_models.IsWall(uid) || _models.IsPanel(uid) ||
               !(_entities.TryGetComponent<OccluderComponent>(uid, out var occluder) && occluder.Enabled);
    }

    private Texture WallTexture(EntityUid uid)
    {
        if (_wallTextures.TryGetValue(uid, out var texture))
            return texture;
        texture = _entities.TryGetComponent<IconComponent>(uid, out var icon) ? _sprites.GetIcon(icon) : _wall;
        _wallTextures[uid] = texture;
        return texture;
    }

    private void CollectBillboards(int width, float horizon, float focal)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(_origin.MapId, _origin.Position, DoomProjection.Range, _nearby);
        foreach (var uid in _nearby)
        {
            if (uid == _player || !_entities.TryGetComponent<SpriteComponent>(uid, out var sprite) ||
                !sprite.Visible || sprite.ContainerOccluded || !IgnoreWall(uid) ||
                !_entities.TryGetComponent<TransformComponent>(uid, out var transform))
                continue;
            if (_models.Add(uid, sprite, transform, _origin.Position, _floor, _objects))
                continue;
            var position = _transforms.GetWorldPosition(transform);
            if (!DoomProjection.Project(position - _origin.Position, _forward, width, out var x, out var depth))
                continue;
            var size = focal / depth;
            var floor = horizon + focal * DoomProjection.EyeHeight / depth;
            var bounds = _sprites.GetLocalBounds((uid, sprite));
            var center = new Vector2(x, floor + bounds.Bottom * size);
            var rect = new UIBox2(x + bounds.Left * size, floor - bounds.Height * size,
                x + bounds.Right * size, floor);
            var direction = (_transforms.GetWorldRotation(transform) - _forward.ToAngle() + Angle.FromDegrees(90)).GetDir();
            _billboards.Add(new Billboard(uid, depth, center, size / EyeManager.PixelsPerMeter,
                rect, direction));
        }
    }

    public bool TryPick(IClydeViewport viewport, Vector2 pixel, out Vector2 position)
    {
        position = default;
        if (_viewport != viewport || _size.X <= 0 || _size.Y <= 0 ||
            pixel.X < 0 || pixel.Y < 0 || pixel.X >= _size.X || pixel.Y >= _size.Y)
            return false;
        var column = Math.Clamp((int) (pixel.X / _size.X * Columns), 0, Columns - 1);
        pixel = pixel / (Vector2) _size * _drawSize;
        var pickedDepth = _depths[column];
        EntityUid picked = default;
        _floor.Pick(pixel, ref picked, ref pickedDepth);
        _objects.Pick(pixel, ref picked, ref pickedDepth);
        foreach (var billboard in _billboards.OrderBy(b => b.Depth))
        {
            if (billboard.Depth < pickedDepth && billboard.Rect.Contains(pixel) &&
                _entities.EntityExists(billboard.Entity))
            {
                position = _transforms.GetWorldPosition(billboard.Entity);
                return true;
            }
        }
        if (picked != default && _entities.EntityExists(picked))
        {
            position = _transforms.GetWorldPosition(picked);
            return true;
        }
        position = _hits[column];
        var belowHorizon = pixel.Y - _drawSize.Y / 2;
        if (belowHorizon > 0)
        {
            var depth = DoomProjection.EyeHeight * DoomProjection.FocalLength(_drawSize.X) / belowHorizon;
            if (depth < _depths[column] && depth < DoomProjection.Range)
                position = _origin.Position + (_forward + DoomProjection.Right(_forward) *
                    ((pixel.X / _drawSize.X * 2 - 1) * DoomProjection.Plane)) * depth;
        }
        return true;
    }

    private readonly record struct Billboard(EntityUid Entity, float Depth, Vector2 Center, float Scale,
        UIBox2 Rect, Direction Direction);
}
