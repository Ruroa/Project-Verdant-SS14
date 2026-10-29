using System.Numerics;
using Robust.Shared.Light;
using Robust.Shared.Map;

namespace Content.Client._PV.DoomView;

internal sealed class DoomLighting
{
    private readonly LightLevelSystem _lightLevels;
    private readonly Dictionary<Vector2i, Color> _samples = new();
    private MapId _map;

    public DoomLighting(IEntityManager entities)
    {
        _lightLevels = entities.System<LightLevelSystem>();
    }

    public void Begin(MapId map)
    {
        _map = map;
        _samples.Clear();
    }

    public Color Sample(Vector2 world)
    {
        // One lighting sample per tile keeps projection overhead low.
        var cell = new Vector2i((int) MathF.Floor(world.X), (int) MathF.Floor(world.Y));
        if (_samples.TryGetValue(cell, out var color))
            return color;

        var sample = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
        color = _lightLevels.CalculateLightColor(new MapCoordinates(sample, _map));
        color = new Color(ToDisplayLight(color.R), ToDisplayLight(color.G), ToDisplayLight(color.B), color.A);
        _samples[cell] = color;
        return color;
    }

    public float Brightness(Color color) => _lightLevels.ColorToLevel(color);

    private static float ToDisplayLight(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value <= 0.0031308f
            ? value * 12.92f
            : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
    }
}
