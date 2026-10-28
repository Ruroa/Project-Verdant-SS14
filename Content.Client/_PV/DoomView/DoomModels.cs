using System.Linq;
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
    private static readonly ProtoId<TagPrototype> DiagonalTag = "Diagonal";
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
        var fixture = sprite.DrawDepth == (int) DrawDepth.WallMountedItems ||
                      spritePath.Contains("/Lighting/", StringComparison.OrdinalIgnoreCase) ||
                      spritePath.Contains("/Lights/", StringComparison.OrdinalIgnoreCase) ||
                      spritePath.Contains("/Camera", StringComparison.OrdinalIgnoreCase);
        var fullWindow = _tags.HasTag(uid, WindowTag) &&
                         !spritePath.Contains("directional", StringComparison.OrdinalIgnoreCase);
        var windoor = _entities.HasComponent<DoorComponent>(uid) &&
                      spritePath.Contains("Windoors", StringComparison.OrdinalIgnoreCase);
        var grille = spritePath.Contains("/Walls/grille", StringComparison.OrdinalIgnoreCase) ||
                     spritePath.Contains("/Walls/clockwork_grille", StringComparison.OrdinalIgnoreCase);
        var girder = spritePath.Contains("wall_girder", StringComparison.OrdinalIgnoreCase) ||
                     spritePath.Contains("/Walls/solid", StringComparison.OrdinalIgnoreCase) &&
                     sprite.AllLayers.Any(layer => layer.RsiState.ToString().Contains("girder", StringComparison.OrdinalIgnoreCase));
        if (item)
            return false; // Keep loose items as directional sprites on the ground.
        var flat = !panel && !fixture && !grille && !girder &&
                   (_entities.HasComponent<SubFloorHideComponent>(uid) ||
                    sprite.DrawDepth <= (int) DrawDepth.HighFloorObjects);
        var width = Math.Clamp(bounds.Width, 0.2f, 1.2f);
        var length = Math.Clamp(bounds.Height, 0.2f, 1.2f);
        var height = _entities.HasComponent<ClimbableComponent>(uid) || _entities.HasComponent<StrapComponent>(uid)
            ? 0.45f : sprite.DrawDepth == (int) DrawDepth.LargeObjects ? 1.05f : 0.75f;
        if (fixture)
        {
            width = Math.Clamp(width, 0.25f, 0.8f);
            length = 0.012f;
            height = Math.Clamp(bounds.Height, 0.25f, 0.65f);
            position += rotation.RotateVec(new Vector2(0, -0.46f));
        }
        // A grille occupies the same tile as a window, and a girder is a smaller
        // structural block inside it. Neither should look like low furniture.
        if (grille || girder)
        {
            width = girder ? 0.82f : 0.91f;
            length = girder ? 0.82f : 0.91f;
            height = girder ? DoomProjection.WallHeight * 0.94f : DoomProjection.WallHeight * 0.98f;
        }
        // A generic 2D decoration is safer as a facing sprite than as a box
        // with unrelated textures stretched over all six sides.
        var modeledProp = spritePath.Contains("/Machines/", StringComparison.OrdinalIgnoreCase) ||
                          spritePath.Contains("/Furniture/", StringComparison.OrdinalIgnoreCase) ||
                          spritePath.Contains("/Dispensers/", StringComparison.OrdinalIgnoreCase) ||
                          _entities.HasComponent<ClimbableComponent>(uid) ||
                          _entities.HasComponent<StrapComponent>(uid);
        if (!panel && !fixture && !grille && !girder && !flat && !modeledProp)
            return false;

        var glass = false;
        if (panel)
        {
            width = 1;
            // Full windows and airlocks are full tile structures; only quarter
            // windows and windoors sit against one edge of their tile.
            length = fullWindow || !windoor && _entities.HasComponent<DoorComponent>(uid) ? 0.96f : 0.12f;
            height = DoomProjection.WallHeight;
            glass = _tags.HasTag(uid, WindowTag) &&
                !(_entities.TryGetComponent<OccluderComponent>(uid, out var occluder) && occluder.Enabled);
            if (_entities.TryGetComponent<DoorComponent>(uid, out var door))
                glass = !door.Occludes;
            if (windoor || !fullWindow && _tags.HasTag(uid, WindowTag))
                position += rotation.RotateVec(new Vector2(0, -0.43f));
            if (_tags.HasTag(uid, DiagonalTag))
            {
                // Diagonal windows are a face across the tile, not a full glass cube.
                length = 0.12f;
                position = _transforms.GetWorldPosition(transform);
                rotation += Angle.FromDegrees(45);
            }
        }
        if (_tags.HasTag(uid, WindowTag) && _entities.TryGetComponent<IconComponent>(uid, out var icon))
        {
            var texture = _sprites.GetIcon(icon);
            var tint = sprite.Color.WithAlpha(glass ? 0.6f * sprite.Color.A : sprite.Color.A);
            var localViewer = (-rotation).RotateVec(viewer - position);
            var y = localViewer.Y < 0 ? -length / 2 : length / 2;
            // Keep the near face readable from either side of the window.
            var nearLeft = localViewer.Y < 0 ? -width / 2 : width / 2;
            var nearRight = -nearLeft;
            objects.Quad(uid, texture, tint, Point(nearLeft, y, height), Point(nearRight, y, height),
                Point(nearRight, y, 0), Point(nearLeft, y, 0));
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
            // Geometry already rotates to the entity's world orientation. Select the
            // sprite's local artwork to avoid applying that rotation twice.
            var texture = TextureFor(layer, panel || fixture ? Angle.Zero : rotation);
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
                // Camera/light/poster textures mount on a wall, near the top.
                // Reverse the back face so labels aren't mirrored.
                var faceY = localViewer.Y < 0 ? -length / 2 : length / 2;
                var leftX = localViewer.Y < 0 ? -width / 2 : width / 2;
                var rightX = -leftX;
                var centerZ = 1.32f;
                objects.Quad(uid, texture, tint,
                    Point(leftX, faceY, centerZ + height / 2),
                    Point(rightX, faceY, centerZ + height / 2),
                    Point(rightX, faceY, centerZ - height / 2),
                    Point(leftX, faceY, centerZ - height / 2));
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
