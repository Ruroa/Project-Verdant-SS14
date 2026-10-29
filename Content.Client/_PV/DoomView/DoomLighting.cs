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
        _samples[cell] = color;
        return color;
    }

    public float Brightness(Color color) => _lightLevels.ColorToLevel(color);
}
