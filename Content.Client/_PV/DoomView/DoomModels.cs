using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Doors.Components;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.SubFloor;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._PV.DoomView;

internal sealed class DoomModels
{
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transforms;
    private readonly SpriteSystem _sprites;
    private readonly TagSystem _tags;

    public DoomModels(IEntityManager entities)
    {
        _entities = entities;
        _transforms = entities.System<SharedTransformSystem>();
        _sprites = entities.System<SpriteSystem>();
        _tags = entities.System<TagSystem>();
    }

    public bool IsWall(EntityUid uid) => _tags.HasTag(uid, WallTag);

    public bool IsPanel(EntityUid uid) => _entities.HasComponent<DoorComponent>(uid) || _tags.HasTag(uid, WindowTag);

    public bool Add(EntityUid uid, SpriteComponent sprite, TransformComponent transform,
        Vector2 viewer, DoomGeometry floor, DoomGeometry objects)
    {
        if (_entities.HasComponent<MobStateComponent>(uid))
            return false;
        var position = _transforms.GetWorldPosition(transform);
        var rotation = _transforms.GetWorldRotation(transform);
        var bounds = _sprites.GetLocalBounds((uid, sprite));
        var panel = IsPanel(uid);
        var item = _entities.HasComponent<ItemComponent>(uid);
        var spritePath = sprite.BaseRSI?.Path.ToString() ?? string.Empty;
        var fixture = spritePath.Contains("/Lighting/", StringComparison.OrdinalIgnoreCase) ||
                      spritePath.Contains("/Lights/", StringComparison.OrdinalIgnoreCase) ||
                      spritePath.Contains("/Camera", StringComparison.OrdinalIgnoreCase);
        if (item)
            return false; // Keep loose items as grounded directional sprites rather than flat geometry.
        var flat = !panel && !fixture && (_entities.HasComponent<SubFloorHideComponent>(uid) ||
                    sprite.DrawDepth <= (int) DrawDepth.HighFloorObjects);
        var width = Math.Clamp(bounds.Width, 0.2f, 1.2f);
        var length = Math.Clamp(bounds.Height, 0.2f, 1.2f);
        var height = _entities.HasComponent<ClimbableComponent>(uid) || _entities.HasComponent<StrapComponent>(uid)
            ? 0.45f : sprite.DrawDepth == (int) DrawDepth.LargeObjects ? 1.05f : 0.75f;
        if (fixture)
        {
            width = Math.Clamp(width, 0.3f, 0.75f);
            length = 0.012f;
            height = 0.36f;
            position += rotation.RotateVec(new Vector2(0, -0.46f));
        }
        var glass = false;
        if (panel)
        {
            width = 1;
            length = _tags.HasTag(uid, WindowTag) ? 0.96f : 0.10f;
            height = DoomProjection.WallHeight;
            glass = _tags.HasTag(uid, WindowTag) &&
                !(_entities.TryGetComponent<OccluderComponent>(uid, out var occluder) && occluder.Enabled);
            if (_entities.TryGetComponent<DoorComponent>(uid, out var door))
                glass = !door.Occludes;
            var path = sprite.BaseRSI?.Path.ToString() ?? string.Empty;
            if (path.Contains("Windoors", StringComparison.OrdinalIgnoreCase) || path.Contains("directional", StringComparison.OrdinalIgnoreCase))
                position += rotation.RotateVec(new Vector2(0, -0.43f));
        }
        if (_tags.HasTag(uid, WindowTag) && _entities.TryGetComponent<IconComponent>(uid, out var icon))
        {
            var texture = _sprites.GetIcon(icon);
            var tint = sprite.Color.WithAlpha(glass ? 0.6f * sprite.Color.A : sprite.Color.A);
            var localViewer = (-rotation).RotateVec(viewer - position);
            var y = localViewer.Y < 0 ? -length / 2 : length / 2;
            objects.Quad(uid, texture, tint, Point(-width / 2, y, height), Point(width / 2, y, height),
                Point(width / 2, y, 0), Point(-width / 2, y, 0));
            // A glass wall occupies the tile, not an infinitely thin image plane.
            // Render its near side plus both edge faces using the same existing artwork.
            var sideTint = tint * new Color(0.75f, 0.75f, 0.75f);
            var x = localViewer.X < 0 ? -width / 2 : width / 2;
            objects.Quad(uid, texture, sideTint, Point(x, -length / 2, height),
                Point(x, length / 2, height), Point(x, length / 2, 0),
                Point(x, -length / 2, 0));
            return true;
        }
        var number = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible)
                continue;
            var texture = TextureFor(layer, rotation);
            if (texture == null)
                continue;
            var tint = layer.Color * sprite.Color;
            if (glass)
                tint = tint.WithAlpha(tint.A * 0.6f);
            var offset = number++ * 0.001f;
            if (flat)
            {
                floor.Quad(uid, texture, tint, Point(-width / 2, length / 2, 0.02f),
                    Point(width / 2, length / 2, 0.02f), Point(width / 2, -length / 2, 0.02f),
                    Point(-width / 2, -length / 2, 0.02f), 100000 + sprite.DrawDepth);
                continue;
            }
            var localViewer = (-rotation).RotateVec(viewer - position);
            if (fixture)
            {
                var faceY = localViewer.Y < 0 ? -length / 2 : length / 2;
                objects.Quad(uid, texture, tint, Point(-width / 2, faceY, 1.35f + height / 2),
                    Point(width / 2, faceY, 1.35f + height / 2),
                    Point(width / 2, faceY, 1.35f - height / 2),
                    Point(-width / 2, faceY, 1.35f - height / 2));
                continue;
            }
            if (localViewer.Y < -length / 2)
                Face(-width / 2, -length / 2 - offset, width / 2, -length / 2 - offset, tint);
            if (localViewer.Y > length / 2)
                Face(width / 2, length / 2 + offset, -width / 2, length / 2 + offset, tint);
            if (localViewer.X > width / 2)
                Face(width / 2 + offset, -length / 2, width / 2 + offset, length / 2, tint * new Color(0.8f, 0.8f, 0.8f));
            if (localViewer.X < -width / 2)
                Face(-width / 2 - offset, length / 2, -width / 2 - offset, -length / 2, tint * new Color(0.8f, 0.8f, 0.8f));
            if (!panel && !fixture && DoomProjection.EyeHeight > height)
                objects.Quad(uid, texture, tint, Point(-width / 2, length / 2, height + offset),
                    Point(width / 2, length / 2, height + offset), Point(width / 2, -length / 2, height + offset),
                    Point(-width / 2, -length / 2, height + offset));

            void Face(float ax, float ay, float bx, float by, Color color) =>
                objects.Quad(uid, texture, color, Point(ax, ay, height), Point(bx, by, height), Point(bx, by, 0), Point(ax, ay, 0));
        }
        return true;

        Vector3 Point(float x, float y, float z)
        {
            var p = position + rotation.RotateVec(new Vector2(x, y));
            return new Vector3(p, z);
        }
    }

    private static Texture? TextureFor(ISpriteLayer layer, Angle rotation)
    {
        if (layer.ActualRsi is { } rsi && rsi.TryGetState(layer.RsiState, out var state))
            return state.GetFrame(layer.EffectiveDirection(rotation), Math.Clamp(layer.AnimationFrame, 0, state.DelayCount - 1));
        return layer.Texture;
    }
}
