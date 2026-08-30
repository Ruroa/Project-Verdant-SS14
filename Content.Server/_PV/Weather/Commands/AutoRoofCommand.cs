using Content.Server.Administration;
using Content.Server._PV.Weather.Components;
using Content.Shared.Administration;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Weather;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._PV.Weather.Commands;

/// <summary>
/// Calculates roof coverage for the grid the invoking mapper is standing on.
/// Boundary-connected floor is outdoors; floor enclosed by BlockWeather
/// structures is indoors. Explicit roof markers are preserved as overrides.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed partial class AutoRoofCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "pvautoroof";
    public string Description => "Calculates indoor and outdoor roof coverage for your current grid.";
    public string Help => "Usage: pvautoroof";

    private static readonly Vector2i[] CardinalDirections =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    };

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        if (shell.Player?.AttachedEntity is not { Valid: true } player)
        {
            shell.WriteError("You must run this command while attached to an entity on the target grid.");
            return;
        }

        var xform = _entities.GetComponent<TransformComponent>(player);
        if (xform.GridUid is not { } gridUid ||
            !_entities.TryGetComponent(gridUid, out MapGridComponent? grid))
        {
            shell.WriteError("You are not standing on a grid.");
            return;
        }

        if (_entities.HasComponent<ImplicitRoofComponent>(gridUid))
        {
            shell.WriteError("This grid uses an implicit roof and does not need automatic roof coverage.");
            return;
        }

        var map = _entities.System<SharedMapSystem>();
        var roofSystem = _entities.System<SharedRoofSystem>();
        var roof = _entities.EnsureComponent<RoofComponent>(gridUid);
        var overrides = _entities.EnsureComponent<RoofOverrideComponent>(gridUid);

        var tiles = new HashSet<Vector2i>();
        foreach (var tile in map.GetAllTiles(gridUid, grid))
        {
            if (!tile.Tile.IsEmpty)
                tiles.Add(tile.GridIndices);
        }

        if (tiles.Count == 0)
        {
            shell.WriteError("The current grid has no tiles.");
            return;
        }

        // Remove overrides that no longer point to a real tile.
        overrides.ForceRoof.IntersectWith(tiles);
        overrides.ForceNoRoof.IntersectWith(tiles);

        var blockers = new HashSet<Vector2i>();
        foreach (var tile in tiles)
        {
            var anchored = map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
            while (anchored.MoveNext(out var entity))
            {
                if (!_entities.HasComponent<BlockWeatherComponent>(entity.Value))
                    continue;

                blockers.Add(tile);
                break;
            }
        }

        var outside = new HashSet<Vector2i>();
        var queue = new Queue<Vector2i>();

        foreach (var tile in tiles)
        {
            if (blockers.Contains(tile))
                continue;

            // Any traversable tile touching empty space is connected to the map edge.
            if (TouchesEmptySpace(tile, tiles))
                AddOutside(tile, outside, queue);
        }

        // Roof Disabled is also an outside seed. This lets one marker expose a
        // connected courtyard while surrounding rooms remain roofed.
        foreach (var tile in overrides.ForceNoRoof)
        {
            if (!blockers.Contains(tile))
                AddOutside(tile, outside, queue);
        }

        while (queue.TryDequeue(out var tile))
        {
            foreach (var direction in CardinalDirections)
            {
                var neighbor = tile + direction;
                if (!tiles.Contains(neighbor) || blockers.Contains(neighbor))
                    continue;

                AddOutside(neighbor, outside, queue);
            }
        }

        var inside = new HashSet<Vector2i>();
        foreach (var tile in tiles)
        {
            if (!blockers.Contains(tile) && !outside.Contains(tile))
                inside.Add(tile);
        }
        var roofed = 0;
        var exposed = 0;

        foreach (var tile in tiles)
        {
            bool value;

            if (overrides.ForceRoof.Contains(tile))
                value = true;
            else if (overrides.ForceNoRoof.Contains(tile))
                value = false;
            else if (blockers.Contains(tile))
                value = TouchesInside(tile, inside);
            else
                value = inside.Contains(tile);

            roofSystem.SetRoof((gridUid, grid, roof), tile, value);

            if (value)
                roofed++;
            else
                exposed++;
        }

        shell.WriteLine($"Automatic roof calculation complete: {roofed} roofed tiles, {exposed} outdoor tiles, " +
                        $"{overrides.ForceRoof.Count + overrides.ForceNoRoof.Count} manual overrides preserved.");
    }

    private static void AddOutside(Vector2i tile, HashSet<Vector2i> outside, Queue<Vector2i> queue)
    {
        if (outside.Add(tile))
            queue.Enqueue(tile);
    }

    private static bool TouchesEmptySpace(Vector2i tile, HashSet<Vector2i> tiles)
    {
        foreach (var direction in CardinalDirections)
        {
            if (!tiles.Contains(tile + direction))
                return true;
        }

        return false;
    }

    private static bool TouchesInside(Vector2i tile, HashSet<Vector2i> inside)
    {
        foreach (var direction in CardinalDirections)
        {
            if (inside.Contains(tile + direction))
                return true;
        }

        return false;
    }
}
