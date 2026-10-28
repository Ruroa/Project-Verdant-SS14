using System.Linq;
using System.Numerics;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client._PV.DoomView;

/// <summary>
/// Approximate local light sampling for the experimental 3D projection.
/// The native 2D lighting buffer cannot be reused on projected 3D surfaces.
/// </summary>
internal sealed class DoomLighting
{
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transforms;
    private readonly SharedPhysicsSystem _physics;
    private readonly List<Light> _lights = new();
    private readonly Dictionary<Vector2i, Color> _samples = new();
    private readonly ILightManager _manager = IoCManager.Resolve<ILightManager>();
    private MapId _map;

    public DoomLighting(IEntityManager entities)
    {
        _entities = entities;
        _transforms = entities.System<SharedTransformSystem>();
        _physics = entities.System<SharedPhysicsSystem>();
    }

    public void Begin(MapId map, IEnumerable<EntityUid> nearby)
    {
        _map = map;
        _lights.Clear();
        _samples.Clear();
        if (!_manager.Enabled || !_manager.DrawLighting)
            return;

        foreach (var uid in nearby)
        {
            if (!_entities.TryGetComponent<PointLightComponent>(uid, out var light) ||
                !light.Enabled || light.ContainerOccluded || light.Radius <= 0 || light.Energy <= 0 ||
                !_entities.TryGetComponent<TransformComponent>(uid, out var transform))
                continue;

            var position = _transforms.GetWorldPosition(transform) +
                           _transforms.GetWorldRotation(transform).RotateVec(light.Offset);
            _lights.Add(new Light(uid, position, light.Radius, light.Energy, light.Color));
        }
    }

    public Color Sample(Vector2 world)
    {
        if (!_manager.Enabled || !_manager.DrawLighting)
            return Color.White;

        // Half-tile resolution reduces the number of visibility rays needed.
        var cell = new Vector2i((int) MathF.Floor(world.X * 2), (int) MathF.Floor(world.Y * 2));
        if (_samples.TryGetValue(cell, out var color))
            return color;

        var sample = new Vector2((cell.X + 0.5f) * 0.5f, (cell.Y + 0.5f) * 0.5f);
        var r = 0.075f;
        var g = 0.078f;
        var b = 0.09f;
        foreach (var light in _lights)
        {
            var delta = sample - light.Position;
            var distance = delta.Length();
            if (distance >= light.Radius)
                continue;
            if (distance > 0.25f && Blocked(light, sample, delta, distance))
                continue;

            var t = 1f - distance / light.Radius;
            var strength = MathF.Min(2f, light.Energy) * t * t * 1.8f;
            r += light.Color.R * strength;
            g += light.Color.G * strength;
            b += light.Color.B * strength;
        }

        color = new Color(Math.Clamp(r, 0.025f, 1f),
            Math.Clamp(g, 0.025f, 1f), Math.Clamp(b, 0.025f, 1f));
        _samples[cell] = color;
        return color;
    }

    private bool Blocked(Light light, Vector2 target, Vector2 delta, float distance)
    {
        var ray = new CollisionRay(light.Position, delta / distance,
            (int) (CollisionGroup.Opaque | CollisionGroup.Impassable | CollisionGroup.InteractImpassable));
        var hit = _physics.IntersectRayWithPredicate(_map, ray, MathF.Max(0.01f, distance - 0.12f),
            uid => uid == light.Entity ||
                   !_entities.TryGetComponent<OccluderComponent>(uid, out var occluder) ||
                   !occluder.Enabled, false).FirstOrDefault();
        return hit.HitEntity != default;
    }

    private readonly record struct Light(EntityUid Entity, Vector2 Position, float Radius, float Energy, Color Color);
}
