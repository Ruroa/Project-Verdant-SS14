using System.Numerics;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Light;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Client.GameObjects;

namespace Content.Client._PV.DoomView;

internal sealed class DoomLighting
{
    private const float MaxLightRadius = 16f;
    private const float DisplayExposure = 1.3f;
    private static readonly Color AmbientFloor = new(0.12f, 0.13f, 0.15f);
    private readonly LightLevelSystem _lightLevels;
    private readonly IEntityManager _entities;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedPhysicsSystem _physics;
    private readonly SharedTransformSystem _transforms;
    private readonly IPrototypeManager _prototypes;
    private readonly Dictionary<Vector2i, Color> _samples = new();
    private readonly HashSet<Entity<PointLightComponent>> _nearbyLights = new();
    private MapId _map;

    public DoomLighting(IEntityManager entities)
    {
        _entities = entities;
        _lightLevels = entities.System<LightLevelSystem>();
        _lookup = entities.System<EntityLookupSystem>();
        _physics = entities.System<SharedPhysicsSystem>();
        _transforms = entities.System<SharedTransformSystem>();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
    }

    public void Begin(MapId map)
    {
        _map = map;
        _samples.Clear();
    }

    public Color Sample(Vector2 world)
    {
        var cell = new Vector2i((int) MathF.Floor(world.X), (int) MathF.Floor(world.Y));
        if (_samples.TryGetValue(cell, out var color))
            return color;

        var sample = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
        var coordinates = new MapCoordinates(sample, _map);
        if (!_lightLevels.TryCalculateLightColor(coordinates, out color,
                new LightLevelQueryOptions(ShadowCastingOnly: true)))
            color = Color.Black;
        _nearbyLights.Clear();
        _lookup.GetEntitiesInRange<PointLightComponent>(coordinates, MaxLightRadius, _nearbyLights);
        foreach (var lightEntity in _nearbyLights)
        {
            var light = lightEntity.Comp;
            if (light.CastShadows || !light.Enabled || light.ContainerOccluded ||
                !_entities.TryGetComponent(lightEntity.Owner, out TransformComponent? transform))
                continue;

            var rotation = _transforms.GetWorldRotation(transform);
            var lightPosition = _transforms.GetWorldPosition(transform) + rotation.RotateVec(light.Offset);
            var delta = sample - lightPosition;
            var distanceSquared = delta.LengthSquared();
            var radius = Math.Min(light.Radius, MaxLightRadius);
            if (distanceSquared > radius * radius || !VisibleFromLight(lightEntity.Owner, lightPosition, delta))
                continue;

            var sqrtDistance = distanceSquared + 1f;
            var s = Math.Clamp(MathF.Sqrt(sqrtDistance) / radius, 0f, 1f);
            var s2 = s * s;
            var curve = MathHelper.Lerp(s, s2, Math.Clamp(light.CurveFactor, 0f, 1f));
            var attenuation = ((1f - s2) * (1f - s2)) / (1f + light.Falloff * curve);
            var mask = GetMaskFactor(light, rotation, delta);
            var contribution = light.Color.RGBA * (light.Energy * attenuation * mask);
            color = new Color(color.RGBA + contribution);
        }

        color = new Color(
            ToDisplayLight(Math.Max(color.R, AmbientFloor.R) * DisplayExposure),
            ToDisplayLight(Math.Max(color.G, AmbientFloor.G) * DisplayExposure),
            ToDisplayLight(Math.Max(color.B, AmbientFloor.B) * DisplayExposure),
            color.A);
        _samples[cell] = color;
        return color;
    }

    public float Brightness(Color color) => _lightLevels.ColorToLevel(color);

    private bool VisibleFromLight(EntityUid light, Vector2 position, Vector2 delta)
    {
        var distance = delta.Length();
        if (distance <= 0.05f)
            return true;

        var ray = new CollisionRay(position, delta / distance,
            (int) (CollisionGroup.Opaque | CollisionGroup.Impassable | CollisionGroup.InteractImpassable));
        foreach (var hit in _physics.IntersectRayWithPredicate(_map, ray, distance,
                     IsLightBlocker, false))
        {
            if (hit.HitEntity != light && hit.Distance < distance - 0.55f)
                return false;
        }

        return true;
    }

    private bool IsLightBlocker(EntityUid uid)
    {
        return _entities.TryGetComponent<OccluderComponent>(uid, out var occluder) && occluder.Enabled;
    }

    private float GetMaskFactor(SharedPointLightComponent light, Angle worldRotation, Vector2 delta)
    {
        if (light.LightMask is not { } maskId || !_prototypes.TryIndex(maskId, out var mask))
            return 1f;

        var maskRotation = SharedPointLightSystem.GetMaskWorldRotation(light, worldRotation);
        var relativeAngle = MathHelper.CloseTo(delta.LengthSquared(), 0)
            ? Angle.Zero
            : Angle.FromWorldVec(delta) - maskRotation;
        var factor = 0d;
        foreach (var cone in mask.LightCones)
        {
            var difference = Math.Abs(Angle.ShortestDistance(relativeAngle, cone.Direction));
            if (difference <= cone.InnerWidth)
                return 1f;
            if (difference < cone.OuterWidth)
                factor = Math.Max(factor,
                    (cone.OuterWidth.Theta - difference) / (cone.OuterWidth.Theta - cone.InnerWidth.Theta));
        }

        return (float) factor;
    }

    private static float ToDisplayLight(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value <= 0.0031308f
            ? value * 12.92f
            : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
    }
}
