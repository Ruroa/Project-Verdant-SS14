using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Light;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._PV.DoomView;

internal sealed class DoomLighting
{
    private readonly IEntityManager _entities;
    private readonly LightLevelSystem _lightLevels;
    private readonly SharedTransformSystem _transforms;
    private readonly Dictionary<Vector2i, Color> _samples = new();
    private readonly List<(PointLightComponent Light, Angle Rotation)> _rotatedLights = new();
    private MapId _map;

    public DoomLighting(IEntityManager entities)
    {
        _entities = entities;
        _lightLevels = entities.System<LightLevelSystem>();
        _transforms = entities.System<SharedTransformSystem>();
    }

    public void Begin(MapId map, EntityUid player, Vector2 forward, IEnumerable<EntityUid> nearby)
    {
        RestoreLightRotations();
        _map = map;
        _samples.Clear();

        if (forward.LengthSquared() < 0.0001f)
            return;

        var facing = Angle.FromWorldVec(Vector2.Normalize(forward));
        foreach (var uid in nearby)
        {
            if (!_entities.TryGetComponent<PointLightComponent>(uid, out var light) || !light.MaskAutoRotate ||
                !IsAttachedTo(uid, player) || !_entities.TryGetComponent<TransformComponent>(uid, out var transform))
                continue;

            _rotatedLights.Add((light, light.Rotation));
            light.Rotation = facing - _transforms.GetWorldRotation(transform);
        }
    }

    public Color Sample(Vector2 world)
    {
        // One lighting sample per tile keeps projection overhead low.
        var cell = new Vector2i((int) MathF.Floor(world.X), (int) MathF.Floor(world.Y));
        if (_samples.TryGetValue(cell, out var color))
            return color;

        var sample = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
        color = _lightLevels.CalculateLightColor(new MapCoordinates(sample, _map));
        _samples[cell] = color;
        return color;
    }

    public float Brightness(Color color) => _lightLevels.ColorToLevel(color);

    public void End() => RestoreLightRotations();

    private bool IsAttachedTo(EntityUid uid, EntityUid player)
    {
        var current = uid;
        while (current != player && _entities.TryGetComponent<TransformComponent>(current, out var transform))
        {
            if (transform.ParentUid == current)
                return false;
            current = transform.ParentUid;
        }

        return current == player;
    }

    private void RestoreLightRotations()
    {
        foreach (var (light, rotation) in _rotatedLights)
            light.Rotation = rotation;
        _rotatedLights.Clear();
    }
}
