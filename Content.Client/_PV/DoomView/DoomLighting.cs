using System.Linq;
using System.Numerics;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
    private readonly SharedMapSystem _maps;
    private Color _ambient;
    private readonly List<Light> _lights = new();
    private readonly Dictionary<Vector2i, Color> _samples = new();
    private readonly ILightManager _manager = IoCManager.Resolve<ILightManager>();
    private MapId _map;

    public DoomLighting(IEntityManager entities)
    {
        _entities = entities;
        _transforms = entities.System<SharedTransformSystem>();
        _physics = entities.System<SharedPhysicsSystem>();
        _maps = entities.System<SharedMapSystem>();
    }

    public void Begin(MapId map, IEnumerable<EntityUid> nearby)
    {
        _map = map;
        // Respect the map's own ambient light (sunlit maps stay bright).
        var mapUid = _maps.GetMap(map);
        _ambient = _entities.TryGetComponent<MapLightComponent>(mapUid, out var mapLight)
            ? mapLight.AmbientLightColor
            : MapLightComponent.DefaultColor;
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
        // This is a software approximation of SS14's light buffer. A modest
        // indirect-light floor keeps normal textures legible without making
        // unpowered rooms look illuminated.
        var r = MathF.Max(0.115f, _ambient.R);
        var g = MathF.Max(0.12f, _ambient.G);
        var b = MathF.Max(0.135f, _ambient.B);
        foreach (var light in _lights)
        {
            var delta = sample - light.Position;
            var distance = delta.Length();
            if (distance >= light.Radius)
                continue;
            if (distance > 0.25f && Blocked(light, delta, distance))
                continue;

            var t = 1f - distance / light.Radius;
            // The old squared attenuation extinguished most station light
            // before it reached the floor; use smoother falloff.
            var strength = MathF.Min(2.5f, light.Energy) * t * (0.65f + 0.35f * t) * 1.5f;
            r += light.Color.R * strength;
            g += light.Color.G * strength;
            b += light.Color.B * strength;
        }

        color = new Color(Math.Clamp(r, 0.025f, 1f),
            Math.Clamp(g, 0.025f, 1f), Math.Clamp(b, 0.025f, 1f));
        _samples[cell] = color;
        return color;
    }

    private bool Blocked(Light light, Vector2 delta, float distance)
    {
        // Wall-mounted lights can sit almost exactly on a wall collider.
        // Starting the ray a little toward the target avoids mistakenly
        // treating the mounting wall as blocking its own illumination.
        var direction = delta / distance;
        var bias = MathF.Min(0.16f, distance * 0.3f);
        var ray = new CollisionRay(light.Position + direction * bias, direction,
            (int) (CollisionGroup.Opaque | CollisionGroup.Impassable | CollisionGroup.InteractImpassable));
        var hit = _physics.IntersectRayWithPredicate(_map, ray, MathF.Max(0.01f, distance - bias - 0.12f),
            uid => uid == light.Entity ||
                   !_entities.TryGetComponent<OccluderComponent>(uid, out var occluder) ||
                   !occluder.Enabled, false).FirstOrDefault();
        return hit.HitEntity != default;
    }

    private readonly record struct Light(EntityUid Entity, Vector2 Position, float Radius, float Energy, Color Color);
}
